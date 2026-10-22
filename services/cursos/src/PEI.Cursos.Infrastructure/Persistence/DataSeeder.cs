using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PEI.Cursos.Domain.Entities;

namespace PEI.Cursos.Infrastructure.Persistence;

/* Componente de infraestructura responsable de sembrar datos iniciales deterministas
en la base de datos pei_cursos_db durante el inicio de la aplicación en modo Development.
Garantiza idempotencia y abastece a Evaluaciones, Frontend y QA con identificadores predecibles. */
public static class DataSeeder
{
    // Identificadores canónicos acordados para la Institución A (Alfa)
    public static readonly Guid InstitucionAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DocenteAId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    public static readonly Guid Alumno1AId = Guid.Parse("11111111-0000-0000-0000-000000000003");
    public static readonly Guid Alumno2AId = Guid.Parse("11111111-0000-0000-0000-000000000004");

    public static readonly Guid CursoA1Id = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    public static readonly Guid CohorteA1Id = Guid.Parse("33333333-3333-3333-3333-333333333333"); // Coincide con CohorteDemoId de EV-08
    public static readonly Guid MatriculacionA1Id = Guid.Parse("aaaaaaaa-0000-0000-0001-000000000001");
    public static readonly Guid MatriculacionA2Id = Guid.Parse("aaaaaaaa-0000-0000-0001-000000000002");

    public static readonly Guid CursoA2Id = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    // Identificadores canónicos acordados para la Institución B (Beta)
    public static readonly Guid InstitucionBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DocenteBId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    public static readonly Guid Alumno1BId = Guid.Parse("22222222-0000-0000-0000-000000000003");
    public static readonly Guid Alumno2BId = Guid.Parse("22222222-0000-0000-0000-000000000004");

    public static readonly Guid CursoB1Id = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    public static readonly Guid CohorteB1Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid MatriculacionB1Id = Guid.Parse("bbbbbbbb-0000-0000-0002-000000000001");
    public static readonly Guid MatriculacionB2Id = Guid.Parse("bbbbbbbb-0000-0000-0002-000000000002");

    public static readonly Guid CursoB2Id = Guid.Parse("cccccccc-0000-0000-0000-000000000004");

    /* Ejecuta la siembra de datos de forma idempotente.
    Usa IgnoreQueryFilters para inspeccionar la base completa sin restricciones de tenant. */
    public static async Task SembrarAsync(
        CursosDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        // Se valida existencia global para garantizar que la operación sea idempotente
        if (await db.Cursos.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("La base de datos de Cursos ya contiene registros. Se omite la siembra inicial.");
            return;
        }

        logger.LogInformation("Iniciando siembra de datos semilla en Cursos...");

        // 1. Institución A: Curso principal con cohorte y alumnos matriculados
        var cursoA1 = new Curso(InstitucionAId, "Programación I", "Programación");
        cursoA1.AsignarId(CursoA1Id);

        var cohorteA1 = new Cohorte(InstitucionAId, CursoA1Id, DocenteAId, "Comisión 1A", "2026-2C");
        cohorteA1.AsignarId(CohorteA1Id);

        var matriculaA1 = new Matriculacion(InstitucionAId, CohorteA1Id, Alumno1AId);
        matriculaA1.AsignarId(MatriculacionA1Id);

        var matriculaA2 = new Matriculacion(InstitucionAId, CohorteA1Id, Alumno2AId);
        matriculaA2.AsignarId(MatriculacionA2Id);

        // Institución A: Curso secundario activo sin cohortes (para probar filtros y listas vacías)
        var cursoA2 = new Curso(InstitucionAId, "Laboratorio de Computación", "Laboratorio");
        cursoA2.AsignarId(CursoA2Id);

        // 2. Institución B: Curso principal con cohorte y alumnos matriculados
        var cursoB1 = new Curso(InstitucionBId, "Bases de Datos", "Datos");
        cursoB1.AsignarId(CursoB1Id);

        var cohorteB1 = new Cohorte(InstitucionBId, CursoB1Id, DocenteBId, "Comisión 1B", "2026-2C");
        cohorteB1.AsignarId(CohorteB1Id);

        var matriculaB1 = new Matriculacion(InstitucionBId, CohorteB1Id, Alumno1BId);
        matriculaB1.AsignarId(MatriculacionB1Id);

        var matriculaB2 = new Matriculacion(InstitucionBId, CohorteB1Id, Alumno2BId);
        matriculaB2.AsignarId(MatriculacionB2Id);

        // Institución B: Curso secundario dado de baja lógica (para validar filtro de activos)
        var cursoB2 = new Curso(InstitucionBId, "Sistemas Operativos", "Sistemas");
        cursoB2.AsignarId(CursoB2Id);
        cursoB2.Desactivar();

        // Agregamos todas las entidades al contexto
        await db.Cursos.AddRangeAsync([cursoA1, cursoA2, cursoB1, cursoB2], ct);
        await db.Cohortes.AddRangeAsync([cohorteA1, cohorteB1], ct);
        await db.Matriculaciones.AddRangeAsync([matriculaA1, matriculaA2, matriculaB1, matriculaB2], ct);

        // Persistencia atómica de todos los registros
        var registrosGuardados = await db.SaveChangesAsync(ct);
        logger.LogInformation("Siembra de Cursos completada exitosamente. Total registros persistidos: {Cantidad}.", registrosGuardados);
    }
}
