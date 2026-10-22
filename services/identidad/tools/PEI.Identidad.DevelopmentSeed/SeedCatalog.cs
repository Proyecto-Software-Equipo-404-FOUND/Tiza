using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.DevelopmentSeed;

public sealed record SeedInstitution(Guid Id, string Nombre, string Codigo);
public sealed record SeedUser(Guid Id, Guid InstitucionId, string Email, string Rol, string Nombre, string Apellido);

public static class SeedCatalog
{
    public const string Version = "ID08-v1";
    public static IReadOnlyList<SeedInstitution> Institutions { get; } = Array.AsReadOnly(new[]
    {
        new SeedInstitution(Guid.Parse("08000000-0000-4000-8000-000000000001"), "Institución de prueba A", "TIZA-TEST-A"),
        new SeedInstitution(Guid.Parse("08000000-0000-4000-8000-000000000002"), "Institución de prueba B", "TIZA-TEST-B")
    });
    public static IReadOnlyList<SeedUser> Users { get; } = Array.AsReadOnly(
        Institutions.SelectMany((institution, index) => new[]
        {
            ("admin", IdentityRoles.Administrador, "Administrador"),
            ("docente", IdentityRoles.Docente, "Docente"),
            ("alumno1", IdentityRoles.Alumno, "Alumno uno"),
            ("alumno2", IdentityRoles.Alumno, "Alumno dos")
        }.Select((role, userIndex) => new SeedUser(
            Guid.Parse($"0800000{index + 1}-0000-4000-8000-{userIndex + 1:D12}"), institution.Id,
            $"{role.Item1}@{(index == 0 ? "a" : "b")}.test", role.Item2, role.Item3,
            index == 0 ? "Prueba A" : "Prueba B"))).ToArray());
}
