using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Identidad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeGlobalEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Todo el Up se ejecuta en la transaccion de EF. El bloqueo impide
            // inserciones concurrentes entre la deteccion de colisiones y el indice.
            migrationBuilder.Sql("""
                LOCK TABLE "Usuario" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Usuario"
                        GROUP BY lower(btrim("Email", U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'))
                        HAVING count(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'H9: existen emails duplicados normalizados; resolver manualmente antes de migrar'
                            USING ERRCODE = '23505', CONSTRAINT = 'IX_Usuario_EmailNormalizado';
                    END IF;
                END; $$;

                CREATE FUNCTION pei_trim_email(value text) RETURNS text
                LANGUAGE sql IMMUTABLE STRICT AS $$
                    SELECT btrim(value, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000');
                $$;
                CREATE FUNCTION pei_normalize_email(value text) RETURNS text
                LANGUAGE sql IMMUTABLE STRICT AS $$ SELECT lower(pei_trim_email(value)); $$;

                CREATE FUNCTION pei_trim_usuario_email() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    NEW."Email" := pei_trim_email(NEW."Email");
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER pei_usuario_email_trim BEFORE INSERT OR UPDATE OF "Email" ON "Usuario"
                    FOR EACH ROW EXECUTE FUNCTION pei_trim_usuario_email();

                -- Solo se recortan datos existentes si el control global no encontro colisiones.
                UPDATE "Usuario" SET "Email" = pei_trim_email("Email")
                    WHERE "Email" IS DISTINCT FROM pei_trim_email("Email");
                """);

            migrationBuilder.DropIndex(
                name: "IX_Usuario_Email",
                table: "Usuario");

            migrationBuilder.AddColumn<string>(
                name: "EmailNormalizado",
                table: "Usuario",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                computedColumnSql: "pei_normalize_email(\"Email\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_EmailNormalizado",
                table: "Usuario",
                column: "EmailNormalizado",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER pei_usuario_email_trim ON "Usuario";
                DROP FUNCTION pei_trim_usuario_email();
                """);
            migrationBuilder.DropIndex(
                name: "IX_Usuario_EmailNormalizado",
                table: "Usuario");

            migrationBuilder.DropColumn(
                name: "EmailNormalizado",
                table: "Usuario");

            migrationBuilder.Sql("""
                DROP FUNCTION pei_normalize_email(text);
                DROP FUNCTION pei_trim_email(text);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_Email",
                table: "Usuario",
                column: "Email",
                unique: true);
        }
    }
}
