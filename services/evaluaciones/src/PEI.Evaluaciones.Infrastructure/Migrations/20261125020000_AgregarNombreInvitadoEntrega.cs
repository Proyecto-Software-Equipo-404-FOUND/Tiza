using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Evaluaciones.Infrastructure.Migrations
{
    /// <summary>
    /// S3-SA-01: guarda el nombre que escribió el invitado al entrar a la sala, para mostrarlo en Resultados
    /// y Métricas. Los alumnos con cuenta quedan en NULL (su nombre sale de Identidad).
    /// </summary>
    public partial class AgregarNombreInvitadoEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NombreInvitado",
                table: "EntregaExamen",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NombreInvitado",
                table: "EntregaExamen");
        }
    }
}
