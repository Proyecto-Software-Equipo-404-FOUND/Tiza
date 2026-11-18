using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Cursos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnaMatriculaActivaPorCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matriculaciones_Cohortes_CohorteId",
                table: "Matriculaciones");

            migrationBuilder.AddColumn<Guid>(
                name: "CursoId",
                table: "Matriculaciones",
                type: "uuid",
                nullable: true);

            // AddColumn mantiene el lock exclusivo sobre Matriculaciones hasta el commit.
            migrationBuilder.Sql("""
                LOCK TABLE "Cohortes" IN SHARE ROW EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Matriculaciones" m
                        LEFT JOIN "Cohortes" c ON c."Id" = m."CohorteId"
                        LEFT JOIN "Cursos" cu ON cu."Id" = c."CursoId"
                        WHERE c."Id" IS NULL OR cu."Id" IS NULL
                           OR m."InstitucionId" IS DISTINCT FROM c."InstitucionId"
                           OR c."InstitucionId" IS DISTINCT FROM cu."InstitucionId"
                    ) THEN
                        RAISE EXCEPTION 'S3-CU-04: existen matrículas con relaciones inconsistentes de institución, curso o cohorte.';
                    END IF;
                END $$;

                UPDATE "Matriculaciones" m SET "CursoId" = c."CursoId"
                FROM "Cohortes" c WHERE c."Id" = m."CohorteId";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Matriculaciones"
                        WHERE "Estado" = 'Activa'
                        GROUP BY "InstitucionId", "CursoId", "AlumnoId"
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'S3-CU-04: existen alumnos con varias matrículas activas por curso. Resolver los conflictos antes de migrar.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CursoId",
                table: "Matriculaciones",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Cohortes_InstitucionId_CursoId_Id",
                table: "Cohortes",
                columns: new[] { "InstitucionId", "CursoId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Matriculaciones_InstitucionId_CursoId_CohorteId",
                table: "Matriculaciones",
                columns: new[] { "InstitucionId", "CursoId", "CohorteId" });

            migrationBuilder.CreateIndex(
                name: "UX_Matriculaciones_Activa_Institucion_Curso_Alumno",
                table: "Matriculaciones",
                columns: new[] { "InstitucionId", "CursoId", "AlumnoId" },
                unique: true,
                filter: "\"Estado\" = 'Activa'");

            migrationBuilder.AddForeignKey(
                name: "FK_Matriculaciones_Cohortes_InstitucionId_CursoId_CohorteId",
                table: "Matriculaciones",
                columns: new[] { "InstitucionId", "CursoId", "CohorteId" },
                principalTable: "Cohortes",
                principalColumns: new[] { "InstitucionId", "CursoId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Matriculaciones_Cohortes_InstitucionId_CursoId_CohorteId",
                table: "Matriculaciones");

            migrationBuilder.DropIndex(
                name: "IX_Matriculaciones_InstitucionId_CursoId_CohorteId",
                table: "Matriculaciones");

            migrationBuilder.DropIndex(
                name: "UX_Matriculaciones_Activa_Institucion_Curso_Alumno",
                table: "Matriculaciones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Cohortes_InstitucionId_CursoId_Id",
                table: "Cohortes");

            migrationBuilder.DropColumn(
                name: "CursoId",
                table: "Matriculaciones");

            migrationBuilder.AddForeignKey(
                name: "FK_Matriculaciones_Cohortes_CohorteId",
                table: "Matriculaciones",
                column: "CohorteId",
                principalTable: "Cohortes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
