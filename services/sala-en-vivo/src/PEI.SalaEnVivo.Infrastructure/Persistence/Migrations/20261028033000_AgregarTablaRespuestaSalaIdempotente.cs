using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.SalaEnVivo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTablaRespuestaSalaIdempotente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AlumnoId",
                table: "ParticipanteSala",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "RespuestasSala",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalaId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlumnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreguntaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Respuesta = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Secuencia = table.Column<int>(type: "integer", nullable: false),
                    RecibidaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestasSala", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespuestasSala_Sala_SalaId",
                        column: x => x.SalaId,
                        principalTable: "Sala",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_respuesta_sala_idempotency",
                table: "RespuestasSala",
                columns: new[] { "SalaId", "AlumnoId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_respuesta_sala_pregunta",
                table: "RespuestasSala",
                columns: new[] { "SalaId", "AlumnoId", "PreguntaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_respuesta_sala_secuencia",
                table: "RespuestasSala",
                columns: new[] { "SalaId", "AlumnoId", "Secuencia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespuestasSala");

            migrationBuilder.DropColumn(
                name: "AlumnoId",
                table: "ParticipanteSala");
        }
    }
}
