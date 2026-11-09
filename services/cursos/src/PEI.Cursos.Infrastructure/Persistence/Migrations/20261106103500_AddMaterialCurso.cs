using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Cursos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Materiales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitucionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CursoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NombreArchivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    CantidadPaginas = table.Column<int>(type: "integer", nullable: false),
                    UbicacionArchivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TextoExtraido = table.Column<string>(type: "text", nullable: false),
                    Estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SubidoPorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaSubidaUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FechaCreacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    FechaModificacionUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materiales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Materiales_Cursos_CursoId",
                        column: x => x.CursoId,
                        principalTable: "Cursos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Materiales_CursoId",
                table: "Materiales",
                column: "CursoId");

            migrationBuilder.CreateIndex(
                name: "IX_Materiales_InstitucionId_CursoId_Activo",
                table: "Materiales",
                columns: new[] { "InstitucionId", "CursoId", "Activo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Materiales");
        }
    }
}
