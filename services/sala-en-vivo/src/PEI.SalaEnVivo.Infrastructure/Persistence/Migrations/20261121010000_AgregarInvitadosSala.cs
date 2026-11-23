using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.SalaEnVivo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// S3-SA-01 (#231): alumno invitado. La sala guarda si admite invitados (por defecto, no)
    /// y cada participante invitado guarda el nombre que escribió.
    /// </summary>
    public partial class AgregarInvitadosSala : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermitirInvitados",
                table: "Sala",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NombreInvitado",
                table: "ParticipanteSala",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermitirInvitados",
                table: "Sala");

            migrationBuilder.DropColumn(
                name: "NombreInvitado",
                table: "ParticipanteSala");
        }
    }
}
