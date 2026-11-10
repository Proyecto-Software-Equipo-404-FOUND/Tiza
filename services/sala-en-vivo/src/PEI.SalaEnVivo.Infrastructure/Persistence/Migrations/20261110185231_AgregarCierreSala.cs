using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.SalaEnVivo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCierreSala : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizadaUtc",
                table: "Sala",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCierre",
                table: "Sala",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalizadaUtc",
                table: "Sala");

            migrationBuilder.DropColumn(
                name: "MotivoCierre",
                table: "Sala");
        }
    }
}
