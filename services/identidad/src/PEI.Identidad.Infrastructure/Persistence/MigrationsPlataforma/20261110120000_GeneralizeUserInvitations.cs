using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Identidad.Infrastructure.Persistence.MigrationsPlataforma;

public partial class GeneralizeUserInvitations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER pei_invitacion_inmutable ON "InvitacionAdministrador";
            ALTER TABLE "EntregaInvitacion"
                DROP CONSTRAINT "FK_EntregaInvitacion_InvitacionAdministrador_InvitacionId";
            ALTER TABLE "InvitacionAdministrador" RENAME TO "InvitacionUsuario";
            ALTER TABLE "InvitacionUsuario" RENAME CONSTRAINT "PK_InvitacionAdministrador" TO "PK_InvitacionUsuario";
            ALTER TABLE "InvitacionUsuario" RENAME CONSTRAINT "FK_Invitacion_Usuario" TO "FK_InvitacionUsuario_Usuario";
            ALTER TABLE "InvitacionUsuario" RENAME CONSTRAINT "CK_Invitacion_Hash" TO "CK_InvitacionUsuario_Hash";
            ALTER INDEX "IX_InvitacionAdministrador_TokenHash" RENAME TO "IX_InvitacionUsuario_TokenHash";
            ALTER INDEX "IX_InvitacionAdministrador_UsuarioId" RENAME TO "IX_InvitacionUsuario_UsuarioId";
            ALTER TABLE "EntregaInvitacion" ADD CONSTRAINT "FK_EntregaInvitacion_InvitacionUsuario_InvitacionId"
                FOREIGN KEY ("InvitacionId") REFERENCES "InvitacionUsuario" ("Id") ON DELETE RESTRICT;

            CREATE FUNCTION pei_invitacion_usuario_inmutable() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."Id" IS DISTINCT FROM OLD."Id" OR NEW."UsuarioId" IS DISTINCT FROM OLD."UsuarioId"
                   OR NEW."TokenHash" IS DISTINCT FROM OLD."TokenHash" OR NEW."Vencimiento" IS DISTINCT FROM OLD."Vencimiento"
                   OR (OLD."Utilizada" IS NOT NULL AND NEW."Utilizada" IS DISTINCT FROM OLD."Utilizada") THEN
                    RAISE EXCEPTION 'Invitación inmutable' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER pei_invitacion_usuario_inmutable BEFORE UPDATE ON "InvitacionUsuario"
                FOR EACH ROW EXECUTE FUNCTION pei_invitacion_usuario_inmutable();
            DROP FUNCTION pei_invitacion_inmutable();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER pei_invitacion_usuario_inmutable ON "InvitacionUsuario";
            DROP FUNCTION pei_invitacion_usuario_inmutable();
            ALTER TABLE "EntregaInvitacion"
                DROP CONSTRAINT "FK_EntregaInvitacion_InvitacionUsuario_InvitacionId";
            ALTER TABLE "InvitacionUsuario" RENAME TO "InvitacionAdministrador";
            ALTER TABLE "InvitacionAdministrador" RENAME CONSTRAINT "PK_InvitacionUsuario" TO "PK_InvitacionAdministrador";
            ALTER TABLE "InvitacionAdministrador" RENAME CONSTRAINT "FK_InvitacionUsuario_Usuario" TO "FK_Invitacion_Usuario";
            ALTER TABLE "InvitacionAdministrador" RENAME CONSTRAINT "CK_InvitacionUsuario_Hash" TO "CK_Invitacion_Hash";
            ALTER INDEX "IX_InvitacionUsuario_TokenHash" RENAME TO "IX_InvitacionAdministrador_TokenHash";
            ALTER INDEX "IX_InvitacionUsuario_UsuarioId" RENAME TO "IX_InvitacionAdministrador_UsuarioId";
            ALTER TABLE "EntregaInvitacion" ADD CONSTRAINT "FK_EntregaInvitacion_InvitacionAdministrador_InvitacionId"
                FOREIGN KEY ("InvitacionId") REFERENCES "InvitacionAdministrador" ("Id") ON DELETE RESTRICT;

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
    }
}
