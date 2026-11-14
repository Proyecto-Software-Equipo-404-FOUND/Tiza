using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using PEI.Identidad.Application.Abstractions;

namespace PEI.Identidad.Application.UseCases.Usuarios.Importacion;

public sealed class ImportarAlumnosHandler(IImportacionAlumnosDataAccess data)
{
    public async Task<ResultadoImportacionAlumnos> HandleAsync(
        Guid administradorId,
        Guid institucionId,
        IReadOnlyList<FilaAlumnoCsv> filas,
        CancellationToken ct)
    {
        var creados = 0;
        var omitidos = 0;
        var errores = new List<ErrorImportacionAlumno>();
        var emailsDelArchivo = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fila in filas)
        {
            ct.ThrowIfCancellationRequested();
            var nombre = fila.Nombre.Trim();
            var apellido = fila.Apellido.Trim();
            var email = fila.Email.Trim();
            var motivo = Validar(fila, nombre, apellido, email);
            if (motivo is not null)
            {
                errores.Add(new(fila.Numero, motivo));
                continue;
            }

            try
            {
                var normalizado = await data.NormalizarEmailAsync(email, ct);
                if (!emailsDelArchivo.Add(normalizado))
                {
                    omitidos++;
                    continue;
                }

                var resultado = await data.CrearPendienteAsync(administradorId, institucionId, nombre, apellido, email, ct);
                if (resultado.Resultado == ResultadoAltaAlumno.Creado) creados++;
                else omitidos++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (UnauthorizedAccessException) { throw; }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex) when (!EsFalloDeConexion(ex))
            {
                errores.Add(new(fila.Numero, "No se pudo crear el alumno y su invitación."));
            }
        }

        return new(creados, omitidos, errores);
    }

    private static string? Validar(FilaAlumnoCsv fila, string nombre, string apellido, string email)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "El nombre es obligatorio.";
        if (nombre.Length > 100) return "El nombre supera los 100 caracteres.";
        if (string.IsNullOrWhiteSpace(apellido)) return "El apellido es obligatorio.";
        if (apellido.Length > 100) return "El apellido supera los 100 caracteres.";
        if (string.IsNullOrWhiteSpace(email)) return "El email es obligatorio.";
        if (email.Length > 150) return "El email supera los 150 caracteres.";
        if (!new EmailAddressAttribute().IsValid(email)) return "El email no tiene un formato válido.";
        return null;
    }

    private static bool EsFalloDeConexion(Exception exception)
    {
        for (Exception? actual = exception; actual is not null; actual = actual.InnerException)
            if (actual is DbException dbException && (dbException.IsTransient || dbException.SqlState?.StartsWith("08", StringComparison.Ordinal) == true) ||
                actual is System.Net.Sockets.SocketException or TimeoutException or IOException)
                return true;
        return false;
    }
}
