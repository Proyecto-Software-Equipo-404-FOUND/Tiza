using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Evaluaciones.Infrastructure.Migrations
{
    /// <summary>
    /// S3-EV-02 (#223): guarda qué docente creó cada evaluación. Las existentes quedan en NULL
    /// (sin dueño) y las ve cualquier docente de la institución.
    /// </summary>
    public partial class AgregarDocenteIdEvaluacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DocenteId",
                table: "Evaluacion",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocenteId",
                table: "Evaluacion");
        }
    }
}
