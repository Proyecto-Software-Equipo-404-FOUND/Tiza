using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.SalaEnVivo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarEntregaPendiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EntregaPendiente",
                table: "Sala",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntregaPendiente",
                table: "Sala");
        }
    }
}
