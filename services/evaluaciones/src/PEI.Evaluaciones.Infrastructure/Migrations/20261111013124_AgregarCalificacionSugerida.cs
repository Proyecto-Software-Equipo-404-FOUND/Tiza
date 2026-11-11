using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Evaluaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCalificacionSugerida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "EntregaExamen",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CalificacionSugerida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntregaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreguntaId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotaSugerida = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Justificacion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DesgloseJson = table.Column<string>(type: "jsonb", nullable: true),
                    FechaRegistro = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalificacionSugerida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalificacionSugerida_EntregaExamen_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "EntregaExamen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalificacionSugerida_EntregaId_PreguntaId",
                table: "CalificacionSugerida",
                columns: new[] { "EntregaId", "PreguntaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalificacionSugerida");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EntregaExamen");
        }
    }
}
