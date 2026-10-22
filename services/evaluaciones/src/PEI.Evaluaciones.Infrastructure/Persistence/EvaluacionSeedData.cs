using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEI.Evaluaciones.Application.Validacion;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Infrastructure.Persistence;

public static class EvaluacionSeedData
{
    // TODO: reemplazar por los UUID fijos de Identidad y Cursos cuando Franco los confirme.
    public static readonly Guid InstitucionDemoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid CohorteDemoId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static readonly Guid EvaluacionPublicadaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid EvaluacionBorradorId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    public static async Task SembrarAsync(
        EvaluacionDbContext db,
        ValidadorPreguntas validador,
        ILogger logger,
        CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;

        var semillas = new[]
        {
            new Evaluacion
            {
                Id = EvaluacionPublicadaId,
                InstitucionId = InstitucionDemoId,
                Titulo = "Examen parcial de Programacion I",
                Tipo = "Examen",
                Estado = EstadoEvaluacion.Publicada,
                CohorteId = CohorteDemoId,
                PreguntasJson = PreguntasExamenPublicado(),
                FechaCreacion = ahora.AddDays(-7),
                FechaActualizacion = ahora.AddDays(-2),
                Version = 2
            },
            new Evaluacion
            {
                Id = EvaluacionBorradorId,
                InstitucionId = InstitucionDemoId,
                Titulo = "Cuestionario de repaso: Bases de datos",
                Tipo = "Cuestionario",
                Estado = EstadoEvaluacion.Borrador,
                CohorteId = null,
                PreguntasJson = PreguntasCuestionarioBorrador(),
                FechaCreacion = ahora.AddDays(-1),
                FechaActualizacion = ahora.AddDays(-1),
                Version = 1
            }
        };

        // Validamos antes de tocar la base: una semilla invalida nunca debe llegar a insertarse.
        foreach (var semilla in semillas)
        {
            var resultado = validador.Validar(semilla.PreguntasJson);
            if (!resultado.EsValido)
            {
                throw new InvalidOperationException(
                    $"La semilla '{semilla.Titulo}' no cumple el esquema: {string.Join(" | ", resultado.Errores)}");
            }
        }

        // Idempotente: solo inserta las que todavia no existen.
        foreach (var semilla in semillas)
        {
            if (!await db.Evaluaciones.AnyAsync(e => e.Id == semilla.Id, ct))
            {
                db.Evaluaciones.Add(semilla);
            }
        }

        var insertadas = await db.SaveChangesAsync(ct);
        logger.LogInformation("Datos semilla de Evaluaciones: {Cantidad} registros nuevos.", insertadas);
    }

    private static string PreguntasExamenPublicado()
    {
        var documento = new
        {
            version = 1,
            preguntas = new object[]
            {
                new
                {
                    id = Id(101),
                    enunciado = "¿Que palabra clave de C# declara una constante en tiempo de compilacion?",
                    tipo = "OpcionMultiple",
                    puntajeMaximo = 2,
                    opciones = Opciones((201, "const", true), (202, "readonly", false), (203, "static", false), (204, "var", false))
                },
                new
                {
                    id = Id(102),
                    enunciado = "¿Que estructura de datos sigue la politica LIFO?",
                    tipo = "OpcionMultiple",
                    puntajeMaximo = 2,
                    opciones = Opciones((205, "Pila", true), (206, "Cola", false), (207, "Lista enlazada", false))
                },
                new
                {
                    id = Id(103),
                    enunciado = "Explica con tus palabras que es una interfaz y para que sirve.",
                    tipo = "Abierta",
                    puntajeMaximo = 6,
                    rubrica = new
                    {
                        criterios = new[]
                        {
                            new { descripcion = "Define correctamente que es una interfaz", puntaje = 2 },
                            new { descripcion = "Incluye un ejemplo concreto", puntaje = 2 },
                            new { descripcion = "Justifica para que se usa en un diseño", puntaje = 2 }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(documento);
    }

    private static string PreguntasCuestionarioBorrador()
    {
        var documento = new
        {
            version = 1,
            preguntas = new object[]
            {
                new
                {
                    id = Id(104),
                    enunciado = "¿Que comando SQL se usa para consultar datos?",
                    tipo = "OpcionMultiple",
                    puntajeMaximo = 1,
                    opciones = Opciones((208, "SELECT", true), (209, "INSERT", false), (210, "DELETE", false))
                },
                new
                {
                    id = Id(105),
                    enunciado = "Describe la diferencia entre clave primaria y clave foranea.",
                    tipo = "Abierta",
                    puntajeMaximo = 4,
                    rubrica = new
                    {
                        criterios = new[]
                        {
                            new { descripcion = "Explica el rol de la clave primaria", puntaje = 2 },
                            new { descripcion = "Explica el rol de la clave foranea", puntaje = 2 }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(documento);
    }

    private static object[] Opciones(params (int Numero, string Texto, bool EsCorrecta)[] opciones)
    {
        return opciones
            .Select(o => (object)new { id = Id(o.Numero), texto = o.Texto, esCorrecta = o.EsCorrecta })
            .ToArray();
    }

    // UUID fijo y legible a partir de un numero: 101 -> 00000000-0000-0000-0000-000000000101
    private static Guid Id(int numero) => Guid.Parse($"00000000-0000-0000-0000-{numero:D12}");
}