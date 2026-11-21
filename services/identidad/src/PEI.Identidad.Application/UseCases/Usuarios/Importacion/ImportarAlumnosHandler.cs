using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;

namespace PEI.Identidad.Application.UseCases.Usuarios.Importacion;

public sealed class ImportarAlumnosHandler(IImportacionAlumnosDataAccess data, IPasswordHasher? hasher = null)
{
    // S2-ID-02 (#135): mismas reglas que el registro (RegisterCommand): 8 caracteres o más y 72 bytes o menos.
    public const int ContrasenaMinima = 8;
    public const int ContrasenaMaximaBytes = 72;

    /// <summary>null si la contraseña inicial es válida; si no, el motivo.</summary>
    public static string? ValidarContrasenaInicial(string contrasena) =>
        contrasena.Length < ContrasenaMinima || System.Text.Encoding.UTF8.GetByteCount(contrasena) > ContrasenaMaximaBytes
            ? $"La contraseña inicial tiene que tener al menos {ContrasenaMinima} caracteres (y no más de {ContrasenaMaximaBytes} bytes)."
            : null;

    public async Task<ResultadoImportacionAlumnos> HandleAsync(
        Guid administradorId,
        Guid institucionId,
        IReadOnlyList<FilaAlumnoCsv> filas,
        CancellationToken ct,
        string? contrasenaInicial = null)
    {
        // Con contraseña inicial los alumnos quedan activos y pueden entrar sin el mail de activación.
        // Se hashea una vez (BCrypt es lento a propósito) y se usa el mismo hash para todas las filas.
        string? passwordHash = null;
        if (!string.IsNullOrEmpty(contrasenaInicial))
        {
            if (ValidarContrasenaInicial(contrasenaInicial) is { } motivoContrasena)
                throw new ArgumentException(motivoContrasena, nameof(contrasenaInicial));
            passwordHash = (hasher ?? throw new InvalidOperationException("Falta el hasher de contraseñas."))
                .Hash(contrasenaInicial);
        }

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

                var resultado = passwordHash is null
                    ? await data.CrearPendienteAsync(administradorId, institucionId, nombre, apellido, email, ct)
                    : await data.CrearActivoAsync(administradorId, institucionId, nombre, apellido, email, passwordHash, ct);
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
