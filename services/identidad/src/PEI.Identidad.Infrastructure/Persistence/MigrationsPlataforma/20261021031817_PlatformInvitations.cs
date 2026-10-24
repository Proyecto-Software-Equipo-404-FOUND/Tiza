using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Identidad.Infrastructure.Persistence.MigrationsPlataforma
{
    /// <inheritdoc />
    public partial class PlatformInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvitacionAdministrador",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Vencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Utilizada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitacionAdministrador", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntregaInvitacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destinatario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContenidoProtegido = table.Column<string>(type: "text", nullable: false),
                    Creada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Entregada = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Intentos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregaInvitacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregaInvitacion_InvitacionAdministrador_InvitacionId",
                        column: x => x.InvitacionId,
                        principalTable: "InvitacionAdministrador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntregaInvitacion_InvitacionId",
                table: "EntregaInvitacion",
                column: "InvitacionId",
                unique: true);

            // Objetos operativos compartidos: deliberadamente no expuestos como repositorios CRUD.
            migrationBuilder.Sql("""
                ALTER TABLE "InvitacionAdministrador" ADD CONSTRAINT "FK_Invitacion_Usuario"
                    FOREIGN KEY ("UsuarioId") REFERENCES "Usuario" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "InvitacionAdministrador" ADD CONSTRAINT "CK_Invitacion_Hash"
                    CHECK ("TokenHash" ~ '^[0-9A-F]{64}$');
                CREATE TABLE "BootstrapPlataforma" (
                    "Id" integer PRIMARY KEY CHECK ("Id" = 1), "CuentaId" uuid NOT NULL,
                    "Operador" varchar(150) NOT NULL, "Fecha" timestamptz NOT NULL);
                CREATE TABLE "AuditoriaPlataforma" (
                    "Id" uuid PRIMARY KEY, "ActorId" uuid NOT NULL, "InstitucionId" uuid NOT NULL,
                    "UsuarioId" uuid NOT NULL, "Evento" varchar(50) NOT NULL, "Fecha" timestamptz NOT NULL);
                CREATE TRIGGER pei_bootstrap_no_update BEFORE UPDATE OR DELETE ON "BootstrapPlataforma"
                    FOR EACH ROW EXECUTE FUNCTION pei_proteger_modificacion_registro_email();
                CREATE TRIGGER pei_bootstrap_no_truncate BEFORE TRUNCATE ON "BootstrapPlataforma"
                    FOR EACH STATEMENT EXECUTE FUNCTION pei_proteger_eliminacion_registro_email();
                CREATE TRIGGER pei_auditoria_no_update BEFORE UPDATE OR DELETE ON "AuditoriaPlataforma"
                    FOR EACH ROW EXECUTE FUNCTION pei_proteger_modificacion_registro_email();
                CREATE TRIGGER pei_auditoria_no_truncate BEFORE TRUNCATE ON "AuditoriaPlataforma"
                    FOR EACH STATEMENT EXECUTE FUNCTION pei_proteger_eliminacion_registro_email();
                CREATE FUNCTION pei_invitacion_inmutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Id" IS DISTINCT FROM OLD."Id" OR NEW."UsuarioId" IS DISTINCT FROM OLD."UsuarioId"
                       OR NEW."TokenHash" IS DISTINCT FROM OLD."TokenHash" OR NEW."Vencimiento" IS DISTINCT FROM OLD."Vencimiento"
                       OR (OLD."Utilizada" IS NOT NULL AND NEW."Utilizada" IS DISTINCT FROM OLD."Utilizada") THEN
                        RAISE EXCEPTION 'Invitación inmutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER pei_invitacion_inmutable BEFORE UPDATE ON "InvitacionAdministrador"
                    FOR EACH ROW EXECUTE FUNCTION pei_invitacion_inmutable();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionAdministrador_TokenHash",
                table: "InvitacionAdministrador",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitacionAdministrador_UsuarioId",
                table: "InvitacionAdministrador",
                column: "UsuarioId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE "InvitacionAdministrador", "BootstrapPlataforma", "AuditoriaPlataforma" IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "InvitacionAdministrador") OR EXISTS (SELECT 1 FROM "BootstrapPlataforma")
                       OR EXISTS (SELECT 1 FROM "AuditoriaPlataforma") THEN
                       RAISE EXCEPTION 'Reversión bloqueada: existen datos operativos de plataforma';
                    END IF;
                END; $$;
                DROP TABLE "BootstrapPlataforma";
                DROP TABLE "AuditoriaPlataforma";
                DROP TRIGGER pei_invitacion_inmutable ON "InvitacionAdministrador";
                DROP FUNCTION pei_invitacion_inmutable();
                """);
            migrationBuilder.DropTable(
                name: "EntregaInvitacion");

            migrationBuilder.DropTable(
                name: "InvitacionAdministrador");
        }
    }
}
