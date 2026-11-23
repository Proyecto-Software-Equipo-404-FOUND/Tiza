using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.SalaEnVivo.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// S3-SA-02 (#239): el reloj del examen arranca al iniciar la sala.
    /// DuracionMinutos guarda la duración elegida al crear (las salas existentes quedan en 120)
    /// e IniciadaUtc el momento en que el docente la inició.
    /// </summary>
    public partial class AgregarInicioYDuracionSala : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DuracionMinutos",
                table: "Sala",
                type: "integer",
                nullable: false,
                defaultValue: 120);

            migrationBuilder.AddColumn<DateTime>(
                name: "IniciadaUtc",
                table: "Sala",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DuracionMinutos",
                table: "Sala");

            migrationBuilder.DropColumn(
                name: "IniciadaUtc",
                table: "Sala");
        }
    }
}
