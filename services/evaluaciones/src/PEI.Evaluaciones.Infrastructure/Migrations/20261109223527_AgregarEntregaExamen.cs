using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Evaluaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEntregaExamen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntregaExamen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlumnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    RespuestasJson = table.Column<string>(type: "jsonb", nullable: false),
                    AlertasFocoJson = table.Column<string>(type: "jsonb", nullable: false),
                    NotaMultipleChoice = table.Column<decimal>(type: "numeric", nullable: true),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FechaEntrega = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregaExamen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregaExamen_Evaluacion_EvaluacionId",
                        column: x => x.EvaluacionId,
                        principalTable: "Evaluacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntregaExamen_EvaluacionId",
                table: "EntregaExamen",
                column: "EvaluacionId");

            migrationBuilder.CreateIndex(
                name: "IX_EntregaExamen_SalaId_AlumnoId",
                table: "EntregaExamen",
                columns: new[] { "SalaId", "AlumnoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntregaExamen");
        }
    }
}
