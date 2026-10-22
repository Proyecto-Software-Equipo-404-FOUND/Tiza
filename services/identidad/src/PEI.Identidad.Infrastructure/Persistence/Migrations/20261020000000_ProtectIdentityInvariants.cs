using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PEI.Identidad.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentidadDbContext))]
[Migration("20261020000000_ProtectIdentityInvariants")]
public sealed partial class ProtectIdentityInvariants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION pei_identity_invariants() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_TABLE_NAME = 'Usuario' THEN
                IF NEW."InstitucionId" IS DISTINCT FROM OLD."InstitucionId" THEN
                    RAISE EXCEPTION 'Institution membership is immutable' USING ERRCODE = '23514';
                END IF;
            ELSE
                IF NEW."UsuarioId" IS DISTINCT FROM OLD."UsuarioId"
                   OR NEW."Token" IS DISTINCT FROM OLD."Token"
                   OR NEW."FechaExpiracion" IS DISTINCT FROM OLD."FechaExpiracion"
                   OR (OLD."Revocado" AND NOT NEW."Revocado") THEN
                    RAISE EXCEPTION 'Refresh token identity is immutable' USING ERRCODE = '23514';
                END IF;
            END IF;
            RETURN NEW;
        END; $$;
        CREATE TRIGGER pei_usuario_invariants BEFORE UPDATE ON "Usuario"
            FOR EACH ROW EXECUTE FUNCTION pei_identity_invariants();
        CREATE TRIGGER pei_token_invariants BEFORE UPDATE ON "RefreshToken"
            FOR EACH ROW EXECUTE FUNCTION pei_identity_invariants();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER pei_token_invariants ON "RefreshToken";
        DROP TRIGGER pei_usuario_invariants ON "Usuario";
        DROP FUNCTION pei_identity_invariants();
        """);
}
