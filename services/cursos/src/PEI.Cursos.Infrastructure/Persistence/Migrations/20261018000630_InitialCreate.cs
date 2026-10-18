using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Cursos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cursos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Area = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FechaCreacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cursos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cohortes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CursoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocenteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Periodo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FechaCreacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cohortes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cohortes_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Matriculaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CohorteId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlumnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FechaCreacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matriculaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matriculaciones_Cohortes_CohorteId",
                        column: x => x.CohorteId,
                        principalTable: "Cohortes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cohortes_CursoId_Periodo_Nombre",
                table: "Cohortes",
                columns: new[] { "CursoId", "Periodo", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cursos_InstitucionId_Nombre",
                table: "Cursos",
                columns: new[] { "InstitucionId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Matriculaciones_CohorteId_AlumnoId",
                table: "Matriculaciones",
                columns: new[] { "CohorteId", "AlumnoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matriculaciones");

            migrationBuilder.DropTable(
                name: "Cohortes");

            migrationBuilder.DropTable(
                name: "Cursos");
        }
    }
}
