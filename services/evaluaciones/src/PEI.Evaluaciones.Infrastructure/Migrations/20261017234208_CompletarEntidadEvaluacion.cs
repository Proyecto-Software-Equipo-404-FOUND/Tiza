using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Evaluaciones.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompletarEntidadEvaluacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CohorteId",
                table: "Evaluacion",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Evaluacion",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Borrador");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaActualizacion",
                table: "Evaluacion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FechaCreacion",
                table: "Evaluacion",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "Evaluacion",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CohorteId",
                table: "Evaluacion");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Evaluacion");

            migrationBuilder.DropColumn(
                name: "FechaActualizacion",
                table: "Evaluacion");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Evaluacion");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "Evaluacion");
        }
    }
}
