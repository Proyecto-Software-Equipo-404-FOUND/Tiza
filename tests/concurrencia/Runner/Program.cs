using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Application.Evaluaciones;
using QA07;

var evidence = new List<object>();
var output = "TestResults/qa07-http.json";
var failures = 0;
try
{
    if (args.Length % 2 != 0) throw new ArgumentException("Usar pares --opcion valor.");
    var options = Enumerable.Range(0, args.Length / 2).ToDictionary(i => args[i * 2], i => args[i * 2 + 1]);
    var allowed = new[] { "--url", "--database", "--institution", "--cohort", "--repetitions", "--id", "--output" };
    QaSupport.Require(options.Keys.All(allowed.Contains), "Opcion desconocida.");
    string Option(string key, string fallback) => options.GetValueOrDefault(key, fallback);
    output = Option("--output", output);
    var database = Option("--database", "pei_evaluaciones_qa07");
    var institution = Guid.Parse(Option("--institution", "70707070-7070-4070-8070-707070707070"));
    var cohort = Guid.Parse(Option("--cohort", "33333333-3333-3333-3333-333333333333"));
    var repetitions = int.Parse(Option("--repetitions", "10"));
    QaSupport.Require(repetitions > 0 && institution != Guid.Empty && cohort != Guid.Empty, "Parametros invalidos.");
    QaSupport.Require(!options.ContainsKey("--id") || repetitions == 1, "Con --id usar --repetitions 1.");
    await using var db = QaSupport.Open(database);
    await QaSupport.VerifyDatabase(db, database);
    using var client = new HttpClient { BaseAddress = new Uri(Option("--url", "http://127.0.0.1:5103")), Timeout = TimeSpan.FromSeconds(30) };
    client.DefaultRequestHeaders.Add("X-Institucion-Id", institution.ToString());
    using var probe = await client.GetAsync("/api/v1/evaluaciones");
    QaSupport.Require(probe.IsSuccessStatusCode, "La API no esta disponible.");
    for (var iteration = 1; iteration <= repetitions; iteration++)
    {
        Guid? id = null;
        var responses = Array.Empty<object>();
        try
        {
            if (options.TryGetValue("--id", out var supplied)) id = Guid.Parse(supplied);
            else
            {
                using var questions = JsonDocument.Parse(QaSupport.Questions);
                using var created = await client.PostAsJsonAsync("/api/v1/evaluaciones", new
                {
                    titulo = "QA-07 " + Guid.NewGuid(), tipo = "Examen", preguntasJson = questions.RootElement
                });
                QaSupport.Require((int)created.StatusCode == 201, "No se pudo crear la evaluacion sintetica.");
                id = (await created.Content.ReadFromJsonAsync<EvaluacionDto>())!.Id;
            }
            var before = await db.Evaluaciones.AsNoTracking().SingleAsync(e => e.Id == id);
            QaSupport.Initial(before, institution);
            var initial = await client.GetFromJsonAsync<EvaluacionDto>($"/api/v1/evaluaciones/{id}");
            QaSupport.Require(initial!.Estado == "Borrador" && initial.Version == 1, "API y DB difieren al iniciar.");
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<(int status, string body)> Send()
            {
                await gate.Task;
                using var response = await client.PostAsJsonAsync($"/api/v1/evaluaciones/{id}/publicar", new { versionEsperada = 1, cohorteId = cohort });
                var body = (int)response.StatusCode is 200 or 409 ? await response.Content.ReadAsStringAsync() : "Cuerpo omitido por sanitizacion";
                return ((int)response.StatusCode, body);
            }
            var a = Send();
            var b = Send();
            gate.SetResult();
            var result = await Task.WhenAll(a, b);
            responses = result.Select((r, i) => (object)new { request = i == 0 ? "A" : "B", r.status, r.body }).ToArray();
            var after = await db.Evaluaciones.AsNoTracking().SingleAsync(e => e.Id == id);
            QaSupport.Require(result.Select(r => r.status).Order().SequenceEqual(new[] { 200, 409 }), "Se requiere exactamente un HTTP 200 y un HTTP 409.");
            QaSupport.Final(before, after, cohort);
            var winner = JsonSerializer.Deserialize<EvaluacionDto>(result.Single(r => r.status == 200).body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            QaSupport.Require(winner.Id == id && winner.Version == 2 && winner.Estado == "Publicada" && winner.CohorteId == cohort, "Respuesta ganadora inconsistente.");
            using var retry = await client.PostAsJsonAsync($"/api/v1/evaluaciones/{id}/publicar", new { versionEsperada = 1, cohorteId = cohort });
            QaSupport.Require((int)retry.StatusCode == 409, "Reintento obsoleto aceptado.");
            var unchanged = await db.Evaluaciones.AsNoTracking().SingleAsync(e => e.Id == id);
            QaSupport.Final(before, unchanged, cohort);
            QaSupport.Require(after.FechaActualizacion == unchanged.FechaActualizacion, "El rechazo modifico datos.");
            evidence.Add(new { iteration, id, status = "PASS", responses, initialVersion = before.Version, finalVersion = after.Version, state = after.Estado.ToString(), cohort = after.CohorteId, contentUnchanged = true, retryStatus = 409 });
            Console.WriteLine($"PASS {iteration}: {id} HTTP {result[0].status}/{result[1].status}, Publicada v2");
        }
        catch (Exception ex)
        {
            failures++;
            var reason = ex is InvalidOperationException ? ex.Message : ex.GetType().Name;
            evidence.Add(new { iteration, id, status = "FAIL", responses, reason });
            Console.WriteLine($"FAIL {iteration}: {reason}");
        }
    }
}
catch (Exception ex)
{
    failures++;
    evidence.Add(new { status = "BLOCKED", reason = ex.GetType().Name });
    Console.Error.WriteLine($"BLOCKED: {ex.GetType().Name}; revisar parametros, conexion, migraciones y API.");
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, failures, runs = evidence }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Evidencia: {output}; fallos: {failures}");
return failures == 0 ? 0 : 1;
