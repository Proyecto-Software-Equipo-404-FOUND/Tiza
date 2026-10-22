using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.DevelopmentSeed;

public sealed record SeedInstitution(Guid Id, string Nombre, string Codigo);
public sealed record SeedUser(Guid Id, Guid InstitucionId, string Email, string Rol, string Nombre, string Apellido);

public static class SeedCatalog
{
    public const string Version = "ID08-v2";
    public static IReadOnlyList<SeedInstitution> Institutions { get; } = Array.AsReadOnly(new[]
    {
        new SeedInstitution(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Institución de prueba A", "TIZA-TEST-A"),
        new SeedInstitution(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Institución de prueba B", "TIZA-TEST-B")
    });
    // Referencias explícitas de DataSeeder de Cursos (origin/main f1ef6b7): institución y
    // posición canónica DocenteA/B, Alumno1A/B y Alumno2A/B, no inferencia por rol global.
    // No hay administrador referenciado por Cursos/Evaluaciones: conservar sus UUID de v1.
    public static IReadOnlyList<SeedUser> Users { get; } = Array.AsReadOnly(new[]
    {
        new SeedUser(Guid.Parse("08000001-0000-4000-8000-000000000001"), Institutions[0].Id,
            "admin@a.test", IdentityRoles.Administrador, "Administrador", "Prueba A"),
        new SeedUser(Guid.Parse("11111111-0000-0000-0000-000000000002"), Institutions[0].Id,
            "docente@a.test", IdentityRoles.Docente, "Docente", "Prueba A"),
        new SeedUser(Guid.Parse("11111111-0000-0000-0000-000000000003"), Institutions[0].Id,
            "alumno1@a.test", IdentityRoles.Alumno, "Alumno uno", "Prueba A"),
        new SeedUser(Guid.Parse("11111111-0000-0000-0000-000000000004"), Institutions[0].Id,
            "alumno2@a.test", IdentityRoles.Alumno, "Alumno dos", "Prueba A"),
        new SeedUser(Guid.Parse("08000002-0000-4000-8000-000000000001"), Institutions[1].Id,
            "admin@b.test", IdentityRoles.Administrador, "Administrador", "Prueba B"),
        new SeedUser(Guid.Parse("22222222-0000-0000-0000-000000000002"), Institutions[1].Id,
            "docente@b.test", IdentityRoles.Docente, "Docente", "Prueba B"),
        new SeedUser(Guid.Parse("22222222-0000-0000-0000-000000000003"), Institutions[1].Id,
            "alumno1@b.test", IdentityRoles.Alumno, "Alumno uno", "Prueba B"),
        new SeedUser(Guid.Parse("22222222-0000-0000-0000-000000000004"), Institutions[1].Id,
            "alumno2@b.test", IdentityRoles.Alumno, "Alumno dos", "Prueba B")
    });
}
