using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.SalaEnVivo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProgresoPreguntaSala : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IndicePreguntaActual",
                table: "Sala",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PreguntaActualId",
                table: "Sala",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndicePreguntaActual",
                table: "Sala");

            migrationBuilder.DropColumn(
                name: "PreguntaActualId",
                table: "Sala");
        }
    }
}
