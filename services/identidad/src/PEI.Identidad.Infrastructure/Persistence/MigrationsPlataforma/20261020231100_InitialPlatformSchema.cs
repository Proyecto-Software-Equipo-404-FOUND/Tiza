using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEI.Identidad.Infrastructure.Persistence.MigrationsPlataforma
{
    /// <inheritdoc />
    public partial class InitialPlatformSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Dependencia explícita: primero las tres migraciones institucionales, en la misma base/esquema.
            migrationBuilder.Sql("SELECT pei_normalize_email('preflight'); LOCK TABLE \"Usuario\" IN SHARE ROW EXCLUSIVE MODE;");
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = '"Usuario"'::regclass AND tgname = 'pei_usuario_invariants')
                       OR NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = '"RefreshToken"'::regclass AND tgname = 'pei_token_invariants') THEN
                        RAISE EXCEPTION 'Aplicar primero las migraciones institucionales completas';
                    END IF;
                END; $$;
                """);
            migrationBuilder.CreateTable(
                name: "CuentaPlataforma",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "AdministradorGlobal"),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNormalizado = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, computedColumnSql: "pei_normalize_email(\"Email\")", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuentaPlataforma", x => x.Id);
                    table.CheckConstraint("CK_CuentaPlataforma_Rol", "\"Rol\" = 'AdministradorGlobal'");
                });

            migrationBuilder.CreateTable(
                name: "RegistroEmail",
                columns: table => new
                {
                    EmailNormalizado = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TipoCuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CuentaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistroEmail", x => x.EmailNormalizado);
                    table.CheckConstraint("CK_RegistroEmail_TipoCuenta", "\"TipoCuenta\" IN ('Institucional', 'Plataforma')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuentaPlataforma_EmailNormalizado",
                table: "CuentaPlataforma",
                column: "EmailNormalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistroEmail_TipoCuenta_CuentaId",
                table: "RegistroEmail",
                columns: new[] { "TipoCuenta", "CuentaId" });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pei_proteger_eliminacion_registro_email()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'No se permite eliminar reservas de correo'
                        USING ERRCODE = '23514';
                END;
                $$;
                """);

                        migrationBuilder.Sql("""
                CREATE TRIGGER pei_registro_email_no_delete
                BEFORE DELETE ON "RegistroEmail"
                FOR EACH ROW
                EXECUTE FUNCTION pei_proteger_eliminacion_registro_email();
                """);

            migrationBuilder.Sql("""
                LOCK TABLE "Usuario" IN SHARE ROW EXCLUSIVE MODE;
                """);

            // Impedir modificaciones directas de reservas existentes.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pei_proteger_modificacion_registro_email()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'No se permite modificar reservas de correo'
                        USING ERRCODE = '23514';
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER pei_registro_email_no_update
                BEFORE UPDATE ON "RegistroEmail"
                FOR EACH ROW
                EXECUTE FUNCTION pei_proteger_modificacion_registro_email();
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pei_reservar_email_usuario()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    correo_normalizado text;
                BEGIN
                    correo_normalizado := pei_normalize_email(NEW."Email");

                    INSERT INTO "RegistroEmail"
                        ("EmailNormalizado", "TipoCuenta", "CuentaId")
                    VALUES
                        (correo_normalizado, 'Institucional', NEW."Id")
                    ON CONFLICT ("EmailNormalizado")
                    DO NOTHING;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM "RegistroEmail"
                        WHERE "EmailNormalizado" = correo_normalizado
                          AND "TipoCuenta" = 'Institucional'
                          AND "CuentaId" = NEW."Id"
                    ) THEN
                        RAISE EXCEPTION 'El correo está reservado por otra cuenta'
                            USING ERRCODE = '23505';
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER pei_usuario_reservar_email
                AFTER INSERT ON "Usuario"
                FOR EACH ROW
                EXECUTE FUNCTION pei_reservar_email_usuario();
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pei_reservar_cambio_email_usuario()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF pei_normalize_email(NEW."Email")
                       IS DISTINCT FROM pei_normalize_email(OLD."Email") THEN

                        INSERT INTO "RegistroEmail"
                            ("EmailNormalizado", "TipoCuenta", "CuentaId")
                        VALUES
                            (
                                pei_normalize_email(NEW."Email"),
                                'Institucional',
                                NEW."Id"
                            )
                        ON CONFLICT ("EmailNormalizado")
                        DO NOTHING;

                        IF NOT EXISTS (
                            SELECT 1
                            FROM "RegistroEmail"
                            WHERE "EmailNormalizado" = pei_normalize_email(NEW."Email")
                              AND "TipoCuenta" = 'Institucional'
                              AND "CuentaId" = NEW."Id"
                        ) THEN
                            RAISE EXCEPTION 'El correo está reservado por otra cuenta'
                                USING ERRCODE = '23505';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER pei_usuario_reservar_cambio_email
                AFTER UPDATE OF "Email" ON "Usuario"
                FOR EACH ROW
                EXECUTE FUNCTION pei_reservar_cambio_email_usuario();
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pei_reservar_email_plataforma()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    correo_normalizado text;
                BEGIN
                    correo_normalizado := pei_normalize_email(NEW."Email");

                    IF TG_OP = 'INSERT'
                       OR correo_normalizado IS DISTINCT FROM pei_normalize_email(OLD."Email") THEN

                        INSERT INTO "RegistroEmail"
                            ("EmailNormalizado", "TipoCuenta", "CuentaId")
                        VALUES
                            (correo_normalizado, 'Plataforma', NEW."Id")
                        ON CONFLICT ("EmailNormalizado")
                        DO NOTHING;

                        IF NOT EXISTS (
                            SELECT 1
                            FROM "RegistroEmail"
                            WHERE "EmailNormalizado" = correo_normalizado
                              AND "TipoCuenta" = 'Plataforma'
                              AND "CuentaId" = NEW."Id"
                        ) THEN
                            RAISE EXCEPTION 'El correo está reservado por otra cuenta'
                                USING ERRCODE = '23505';
                        END IF;

                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            // Reservar el correo al crear una cuenta global.
            migrationBuilder.Sql("""
                CREATE TRIGGER pei_plataforma_reservar_email
                AFTER INSERT ON "CuentaPlataforma"
                FOR EACH ROW
                EXECUTE FUNCTION pei_reservar_email_plataforma();
                """);

            // Reservar el nuevo correo cuando una cuenta global lo modifica.
            migrationBuilder.Sql("""
                CREATE TRIGGER pei_plataforma_reservar_cambio_email
                AFTER UPDATE OF "Email" ON "CuentaPlataforma"
                FOR EACH ROW
                EXECUTE FUNCTION pei_reservar_email_plataforma();
                """);

            migrationBuilder.Sql("""
                INSERT INTO "RegistroEmail"
                    ("EmailNormalizado", "TipoCuenta", "CuentaId")
                SELECT
                    "EmailNormalizado",
                    'Institucional',
                    "Id"
                FROM "Usuario";
                """);

            migrationBuilder.Sql("""
                -- La reserva es histórica y deliberadamente no tiene FK con cascada al propietario.
                -- Solo se admite reservar el correo actual de una identidad real.
                CREATE FUNCTION pei_validar_reserva_email() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."EmailNormalizado" IS DISTINCT FROM pei_normalize_email(NEW."EmailNormalizado")
                       OR NEW."EmailNormalizado" = '' OR NEW."CuentaId" = '00000000-0000-0000-0000-000000000000'::uuid
                       OR NOT (
                         (NEW."TipoCuenta" = 'Institucional' AND EXISTS (
                            SELECT 1 FROM "Usuario" WHERE "Id" = NEW."CuentaId" AND "EmailNormalizado" = NEW."EmailNormalizado"))
                         OR (NEW."TipoCuenta" = 'Plataforma' AND EXISTS (
                            SELECT 1 FROM "CuentaPlataforma" WHERE "Id" = NEW."CuentaId" AND "EmailNormalizado" = NEW."EmailNormalizado"))
                       ) THEN
                       RAISE EXCEPTION 'Reserva de correo inválida' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER pei_registro_email_validar BEFORE INSERT ON "RegistroEmail"
                    FOR EACH ROW EXECUTE FUNCTION pei_validar_reserva_email();
                CREATE TRIGGER pei_registro_email_no_truncate BEFORE TRUNCATE ON "RegistroEmail"
                    FOR EACH STATEMENT EXECUTE FUNCTION pei_proteger_eliminacion_registro_email();
                CREATE FUNCTION pei_plataforma_trim_email() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN NEW."Email" := pei_trim_email(NEW."Email"); RETURN NEW; END; $$;
                CREATE TRIGGER pei_plataforma_trim_email BEFORE INSERT OR UPDATE OF "Email" ON "CuentaPlataforma"
                    FOR EACH ROW EXECUTE FUNCTION pei_plataforma_trim_email();
                CREATE FUNCTION pei_id_cuenta_inmutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Id" IS DISTINCT FROM OLD."Id" THEN
                        RAISE EXCEPTION 'Identidad inmutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END; $$;
                CREATE TRIGGER pei_usuario_id_inmutable BEFORE UPDATE OF "Id" ON "Usuario"
                    FOR EACH ROW EXECUTE FUNCTION pei_id_cuenta_inmutable();
                CREATE TRIGGER pei_plataforma_id_inmutable BEFORE UPDATE OF "Id" ON "CuentaPlataforma"
                    FOR EACH ROW EXECUTE FUNCTION pei_id_cuenta_inmutable();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No perder silenciosamente reservas históricas ni cuentas globales al revertir.
            migrationBuilder.Sql("""
                LOCK TABLE "Usuario", "CuentaPlataforma", "RegistroEmail" IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "RegistroEmail") OR EXISTS (SELECT 1 FROM "CuentaPlataforma") THEN
                        RAISE EXCEPTION 'Reversión bloqueada: existen identidades o reservas históricas';
                    END IF;
                END; $$;
                DROP TRIGGER pei_registro_email_validar ON "RegistroEmail";
                DROP TRIGGER pei_registro_email_no_truncate ON "RegistroEmail";
                DROP FUNCTION pei_validar_reserva_email();
                DROP TRIGGER pei_usuario_id_inmutable ON "Usuario";
                DROP TRIGGER pei_plataforma_id_inmutable ON "CuentaPlataforma";
                DROP FUNCTION pei_id_cuenta_inmutable();
                DROP TRIGGER pei_plataforma_trim_email ON "CuentaPlataforma";
                DROP FUNCTION pei_plataforma_trim_email();
                """);
            // Eliminar la protección contra borrado de reservas.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pei_registro_email_no_delete
                ON "RegistroEmail";
                """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS pei_proteger_eliminacion_registro_email();
                """);

            // Eliminar la protección contra modificaciones de reservas.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pei_registro_email_no_update
                ON "RegistroEmail";
                """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS pei_proteger_modificacion_registro_email();
                """);

            // Eliminar los triggers institucionales.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pei_usuario_reservar_email
                ON "Usuario";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pei_usuario_reservar_cambio_email
                ON "Usuario";
                """);

            // Eliminar los triggers de plataforma.
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pei_plataforma_reservar_email
                ON "CuentaPlataforma";
                """);

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS pei_plataforma_reservar_cambio_email
                ON "CuentaPlataforma";
                """);

            // Eliminar las funciones creadas por esta migración.
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS pei_reservar_email_usuario();
                DROP FUNCTION IF EXISTS pei_reservar_cambio_email_usuario();
                DROP FUNCTION IF EXISTS pei_reservar_email_plataforma();
                """);

            // Eliminar las tablas de plataforma.
            migrationBuilder.DropTable(
                name: "CuentaPlataforma");

            migrationBuilder.DropTable(
                name: "RegistroEmail");
        }
    }
}
