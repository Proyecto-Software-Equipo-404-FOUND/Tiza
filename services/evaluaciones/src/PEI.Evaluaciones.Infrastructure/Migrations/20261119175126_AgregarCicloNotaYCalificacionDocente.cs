using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Evaluaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCicloNotaYCalificacionDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocenteAprobadorId",
                table: "EntregaExamen",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoNota",
                table: "EntregaExamen",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Sugerida");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaAprobacionUtc",
                table: "EntregaExamen",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NotaFinal",
                table: "EntregaExamen",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PuntajeMaximo",
                table: "EntregaExamen",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalificacionDocente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreguntaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PuntajeDocente = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    ComentarioDocente = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DesgloseJson = table.Column<string>(type: "jsonb", nullable: true),
                    FechaEdicionUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalificacionDocente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalificacionDocente_EntregaExamen_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregaExamen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalificacionDocente_EntregaId_PreguntaId",
                table: "CalificacionDocente",
                columns: new[] { "EntregaId", "PreguntaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalificacionDocente");

            migrationBuilder.DropColumn(
                name: "DocenteAprobadorId",
                table: "EntregaExamen");

            migrationBuilder.DropColumn(
                name: "EstadoNota",
                table: "EntregaExamen");

            migrationBuilder.DropColumn(
                name: "FechaAprobacionUtc",
                table: "EntregaExamen");

            migrationBuilder.DropColumn(
                name: "NotaFinal",
                table: "EntregaExamen");

            migrationBuilder.DropColumn(
                name: "PuntajeMaximo",
                table: "EntregaExamen");
        }
    }
}
