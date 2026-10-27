using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Identidad.Infrastructure.Persistence.MigrationsPlataforma
{
    /// <inheritdoc />
    public partial class ScheduleInvitationRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProximoIntento",
                table: "EntregaInvitacion",
                type: "timestamp with time zone",
                nullable: true);

            // Indice de expresion: EF Core no representa COALESCE como clave de indice.
            migrationBuilder.Sql("""
                CREATE INDEX "IX_EntregaInvitacion_PendienteElegible"
                ON "EntregaInvitacion" ((COALESCE("ProximoIntento", "Creada")), "Creada", "Id")
                WHERE "Entregada" IS NULL AND "ContenidoProtegido" <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX \"IX_EntregaInvitacion_PendienteElegible\";");
            migrationBuilder.DropColumn(
                name: "ProximoIntento",
                table: "EntregaInvitacion");
        }
    }
}
