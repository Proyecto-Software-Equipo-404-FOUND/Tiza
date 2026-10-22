using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.DevelopmentSeed;

public sealed class IdentitySeedRunner(IPasswordHasher passwords)
{
    private sealed record NoTenant : ITenantContext { public Guid? InstitucionId => null; }

    public async Task<SeedReport> RunAsync(SeedOptions options, CancellationToken ct = default)
    {
        var connectionOptions = options.Validate();
        await using var connection = new NpgsqlConnection(connectionOptions.ConnectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Execute("SET LOCAL lock_timeout = '15s'; SET LOCAL statement_timeout = '30s'", ct);
        await Execute("SELECT pg_advisory_xact_lock(20061008)", ct);
        // Explicit pg_temp last prevents temporary tables from shadowing the dedicated schema.
        await Execute($"SET LOCAL search_path = \"{options.Schema}\", pg_temp", ct);
        if ((string?)await Scalar("SELECT current_database()", ct) != connectionOptions.Database ||
            (string?)await Scalar("SELECT current_schema()", ct) != options.Schema ||
            (string?)await Scalar("SHOW session_replication_role", ct) != "origin")
            throw new SeedRejectedException("El destino real o la ejecución de triggers no están autorizados.");
        await ValidateSchema(ct);
        // Stabilize existing rows and reservations as well as catalog metadata until commit.
        // This is deliberately a short, offline development operation, never an API path.
        await Execute("""
            LOCK TABLE "Institucion", "Usuario", "RegistroEmail", "CuentaPlataforma",
                "RefreshToken", "InvitacionAdministrador", "EntregaInvitacion",
                "BootstrapPlataforma", "AuditoriaPlataforma" IN SHARE ROW EXCLUSIVE MODE
            """, ct);
        await ValidateSchema(ct);

        var newInstitutions = new List<SeedInstitution>();
        var newUsers = new List<SeedUser>();
        var conflicts = new List<string>();
        foreach (var item in SeedCatalog.Institutions)
        {
            await using var command = Command("""
                SELECT "Id", "Nombre", "Codigo", "Activo" FROM "Institucion"
                WHERE "Id" = @id OR "Codigo" = @code
                """, ("id", item.Id), ("code", item.Codigo));
            await using var reader = await command.ExecuteReaderAsync(ct);
            var count = 0;
            while (await reader.ReadAsync(ct))
            {
                count++;
                if (reader.GetGuid(0) != item.Id || reader.GetString(1) != item.Nombre ||
                    reader.GetString(2) != item.Codigo || !reader.GetBoolean(3)) conflicts.Add(item.Id.ToString());
            }
            if (count == 0) newInstitutions.Add(item);
        }
        foreach (var item in SeedCatalog.Users)
        {
            var exists = false;
            await using (var command = Command("""
                SELECT "Id", "InstitucionId", "EmailNormalizado", "Rol", "Nombre", "Apellido", "Activo", "PasswordHash"
                FROM "Usuario" WHERE "Id" = @id OR "EmailNormalizado" = pei_normalize_email(@email)
                """, ("id", item.Id), ("email", item.Email)))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    exists = true;
                    if (reader.GetGuid(0) != item.Id || reader.GetGuid(1) != item.InstitucionId ||
                        reader.GetString(2) != item.Email || reader.GetString(3) != item.Rol ||
                        reader.GetString(4) != item.Nombre || reader.GetString(5) != item.Apellido || !reader.GetBoolean(6) ||
                        !IsBcrypt(reader.GetString(7))) conflicts.Add(item.Id.ToString());
                }
            }
            await using (var command = Command("""
                SELECT "TipoCuenta", "CuentaId" FROM "RegistroEmail"
                WHERE "EmailNormalizado" = pei_normalize_email(@email)
                """, ("email", item.Email)))
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                var reserved = await reader.ReadAsync(ct);
                if ((reserved && (!exists || reader.GetString(0) != "Institucional" || reader.GetGuid(1) != item.Id)) ||
                    (exists && !reserved)) conflicts.Add(item.Id.ToString());
            }
            if (!exists) newUsers.Add(item);
        }
        if (conflicts.Count > 0)
            throw new SeedRejectedException($"Conflictos={conflicts.Distinct().Count()}; UUIDs: {string.Join(", ", conflicts.Distinct())}.");

        // Validate all missing credentials before the first INSERT. Never echo a supplied value.
        var hashes = new Dictionary<Guid, string>();
        foreach (var user in newUsers)
        {
            if (!options.Passwords.TryGetValue(user.Id, out var password))
                throw new SeedRejectedException($"Falta credencial externa para UUID {user.Id}.");
            try { hashes[user.Id] = passwords.Hash(password); }
            catch (ArgumentException) { throw new SeedRejectedException($"Credencial inválida para UUID {user.Id}."); }
        }
        var now = DateTime.UtcNow;
        foreach (var item in newInstitutions)
            await Execute("""
                INSERT INTO "Institucion" ("Id","Nombre","Codigo","FechaCreacion","Activo")
                VALUES (@id,@name,@code,@now,TRUE)
                """, ct, ("id", item.Id), ("name", item.Nombre), ("code", item.Codigo), ("now", now));
        foreach (var item in newUsers)
            await Execute("""
                INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
                VALUES (@id,@institution,@email,@hash,@name,@surname,@role,@now,TRUE)
                """, ct, ("id", item.Id), ("institution", item.InstitucionId), ("email", item.Email),
                ("hash", hashes[item.Id]), ("name", item.Nombre), ("surname", item.Apellido), ("role", item.Rol), ("now", now));
        foreach (var item in SeedCatalog.Users)
            if ((long)(await Scalar("""
                SELECT count(*) FROM "Usuario" u JOIN "RegistroEmail" r
                  ON r."CuentaId" = u."Id" AND r."TipoCuenta" = 'Institucional'
                  AND r."EmailNormalizado" = u."EmailNormalizado"
                WHERE u."Id" = @id AND u."InstitucionId" = @institution
                """, ct, ("id", item.Id), ("institution", item.InstitucionId)))! != 1)
                throw new SeedRejectedException("Verificación final de relaciones o reservas fallida.");
        await transaction.CommitAsync(ct);
        var created = newInstitutions.Count + newUsers.Count;
        return new(SeedCatalog.Version, created, 10 - created, 0);

        NpgsqlCommand Command(string sql, params (string Key, object Value)[] parameters)
        {
            var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Key, parameter.Value);
            return command;
        }
        async Task Execute(string sql, CancellationToken token, params (string Key, object Value)[] parameters)
        {
            await using var command = Command(sql, parameters);
            await command.ExecuteNonQueryAsync(token);
        }
        async Task<object?> Scalar(string sql, CancellationToken token, params (string Key, object Value)[] parameters)
        {
            await using var command = Command(sql, parameters);
            return await command.ExecuteScalarAsync(token);
        }
        async Task ValidateSchema(CancellationToken token)
        {
            // Use the existing EF models as the column/index/FK manifest, avoiding a second schema model.
            await using var identity = new IdentidadDbContext(new DbContextOptionsBuilder<IdentidadDbContext>()
                .UseNpgsql(connectionOptions.ConnectionString).Options, new NoTenant());
            await using var platform = new PlataformaDbContext(new DbContextOptionsBuilder<PlataformaDbContext>()
                .UseNpgsql(connectionOptions.ConnectionString).Options);
            foreach (var context in new DbContext[] { identity, platform })
            {
                var model = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model.GetRelationalModel();
                foreach (var table in model.Tables)
                {
                    foreach (var column in table.Columns.Where(c => c.Name != "xmin"))
                    {
                        var valid = await Scalar("""
                            SELECT count(*) FROM pg_attribute a JOIN pg_class t ON t.oid=a.attrelid
                            JOIN pg_namespace n ON n.oid=t.relnamespace
                            WHERE n.nspname=@schema AND t.relname=@table AND t.relkind='r'
                              AND a.attname=@column AND a.attnum>0 AND NOT a.attisdropped
                              AND format_type(a.atttypid,a.atttypmod)=@type AND a.attnotnull=@required
                              AND (a.attgenerated='s')=@computed
                              AND (NOT @computed OR translate(replace(pg_get_expr((SELECT d.adbin FROM pg_attrdef d
                                  WHERE d.adrelid=t.oid AND d.adnum=a.attnum),t.oid),'::text',''),'() ','')=translate(@expression,'() ',''))
                            """, token, ("schema", options.Schema), ("table", table.Name), ("column", column.Name),
                            ("type", column.StoreType.Replace("character varying (", "character varying(")),
                            ("required", !column.IsNullable), ("computed", column.PropertyMappings.Any(p => p.Property.GetComputedColumnSql() != null)),
                            ("expression", column.PropertyMappings.Select(p => p.Property.GetComputedColumnSql()).FirstOrDefault(s => s != null) ?? ""));
                        if ((long)valid! != 1) throw new SeedRejectedException($"Prerrequisito de columna ausente o incompatible: {table.Name}.{column.Name}.");
                    }
                    foreach (var index in table.Indexes)
                    {
                        if ((long)(await Scalar("""
                            SELECT count(*) FROM pg_index i JOIN pg_class x ON x.oid=i.indexrelid
                            JOIN pg_class t ON t.oid=i.indrelid JOIN pg_namespace n ON n.oid=t.relnamespace
                            WHERE n.nspname=@schema AND t.relname=@table AND x.relname=@name
                              AND i.indisvalid AND i.indisready AND i.indisunique=@unique
                              AND i.indpred IS NULL AND i.indexprs IS NULL
                              AND ARRAY(SELECT a.attname::text FROM unnest(i.indkey) WITH ORDINALITY k(num,ord)
                                JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.num ORDER BY k.ord)=@columns
                            """, token, ("schema", options.Schema), ("table", table.Name), ("name", index.Name),
                            ("unique", index.IsUnique), ("columns", index.Columns.Select(c => c.Name).ToArray())))! != 1)
                            throw new SeedRejectedException($"Índice requerido ausente o incompatible: {index.Name}.");
                    }
                    foreach (var constraint in table.UniqueConstraints)
                        await RequireConstraint(table.Name, constraint.Name, constraint.GetIsPrimaryKey() ? "p" : "u", token);
                    foreach (var constraint in table.ForeignKeyConstraints)
                    {
                        await RequireConstraint(table.Name, constraint.Name, "f", token);
                        if ((long)(await Scalar("""
                            SELECT count(*) FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
                            JOIN pg_namespace n ON n.oid=t.relnamespace JOIN pg_class p ON p.oid=c.confrelid
                            WHERE n.nspname=@schema AND t.relname=@table AND c.conname=@name
                              AND p.relnamespace=n.oid AND p.relname=@principal AND NOT c.condeferrable
                              AND c.confdeltype::text=@delete
                              AND ARRAY(SELECT a.attname::text FROM unnest(c.conkey) WITH ORDINALITY k(num,ord)
                                JOIN pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.num ORDER BY k.ord)=@columns
                              AND ARRAY(SELECT a.attname::text FROM unnest(c.confkey) WITH ORDINALITY k(num,ord)
                                JOIN pg_attribute a ON a.attrelid=p.oid AND a.attnum=k.num ORDER BY k.ord)=@principalColumns
                            """, token, ("schema", options.Schema), ("table", table.Name), ("name", constraint.Name),
                            ("principal", constraint.PrincipalTable.Name), ("columns", constraint.Columns.Select(c => c.Name).ToArray()),
                            ("principalColumns", constraint.PrincipalColumns.Select(c => c.Name).ToArray()),
                            ("delete", constraint.OnDeleteAction switch
                            {
                                Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade => "c",
                                Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict => "r",
                                Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull => "n",
                                Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetDefault => "d",
                                _ => "a"
                            })))! != 1) throw new SeedRejectedException($"Relación requerida incompatible: {constraint.Name}.");
                    }
                    foreach (var constraint in table.CheckConstraints)
                        await RequireConstraint(table.Name, constraint.Name!, "c", token);
                }
                var history = context == identity ? "__EFMigrationsHistory" : "__EFMigrationsHistoryPlataforma";
                foreach (var migration in context.Database.GetMigrations())
                    if ((long)(await Scalar($"SELECT count(*) FROM \"{history}\" WHERE \"MigrationId\"=@migration", token, ("migration", migration)))! != 1)
                        throw new SeedRejectedException("Faltan migraciones requeridas; el cargador no las aplica.");
            }
            foreach (var (table, trigger, function) in RequiredTriggers)
                if ((long)(await Scalar("""
                    SELECT count(*) FROM pg_trigger g JOIN pg_class t ON t.oid=g.tgrelid
                    JOIN pg_namespace n ON n.oid=t.relnamespace JOIN pg_proc p ON p.oid=g.tgfoid
                    WHERE n.nspname=@schema AND t.relname=@table AND g.tgname=@trigger
                      AND NOT g.tgisinternal AND g.tgenabled IN ('O','A') AND p.proname=@function
                      AND p.pronamespace=n.oid
                    """, token, ("schema", options.Schema), ("table", table), ("trigger", trigger), ("function", function)))! != 1)
                    throw new SeedRejectedException($"Trigger requerido ausente o deshabilitado: {trigger}.");
            // Operational ID-06 tables must exist too, even though the seed never writes them.
            foreach (var table in new[] { "BootstrapPlataforma", "AuditoriaPlataforma" })
                if (await Scalar("SELECT to_regclass(@name)::text", token, ("name", $"\"{options.Schema}\".\"{table}\"")) is DBNull or null)
                    throw new SeedRejectedException("Faltan tablas operativas de ID-06.");
            if ((string?)await Scalar("SELECT pei_normalize_email(E' \\tADMIN@A.TEST ')", token) != "admin@a.test")
                throw new SeedRejectedException("Normalización de correo incompatible.");
        }
        async Task RequireConstraint(string table, string name, string type, CancellationToken token)
        {
            if ((long)(await Scalar("""
                SELECT count(*) FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid
                JOIN pg_namespace n ON n.oid=t.relnamespace
                WHERE n.nspname=@schema AND t.relname=@table AND c.conname=@name
                  AND c.contype::text=@type AND c.convalidated
                """, token, ("schema", options.Schema), ("table", table), ("name", name), ("type", type)))! != 1)
                throw new SeedRejectedException($"Restricción requerida ausente o inválida: {name}.");
        }
    }

    private static bool IsBcrypt(string hash) => System.Text.RegularExpressions.Regex.IsMatch(hash,
        @"^\$2[aby]\$(1[2-9]|2[0-9]|30|31)\$[./A-Za-z0-9]{53}$");

    private static readonly (string Table, string Trigger, string Function)[] RequiredTriggers =
    [
        ("Usuario", "pei_usuario_invariants", "pei_identity_invariants"),
        ("RefreshToken", "pei_token_invariants", "pei_identity_invariants"),
        ("Usuario", "pei_usuario_email_trim", "pei_trim_usuario_email"),
        ("Usuario", "pei_usuario_reservar_email", "pei_reservar_email_usuario"),
        ("Usuario", "pei_usuario_reservar_cambio_email", "pei_reservar_cambio_email_usuario"),
        ("Usuario", "pei_usuario_id_inmutable", "pei_id_cuenta_inmutable"),
        ("CuentaPlataforma", "pei_plataforma_reservar_email", "pei_reservar_email_plataforma"),
        ("CuentaPlataforma", "pei_plataforma_reservar_cambio_email", "pei_reservar_email_plataforma"),
        ("CuentaPlataforma", "pei_plataforma_trim_email", "pei_plataforma_trim_email"),
        ("CuentaPlataforma", "pei_plataforma_id_inmutable", "pei_id_cuenta_inmutable"),
        ("RegistroEmail", "pei_registro_email_no_delete", "pei_proteger_eliminacion_registro_email"),
        ("RegistroEmail", "pei_registro_email_no_update", "pei_proteger_modificacion_registro_email"),
        ("RegistroEmail", "pei_registro_email_no_truncate", "pei_proteger_eliminacion_registro_email"),
        ("RegistroEmail", "pei_registro_email_validar", "pei_validar_reserva_email"),
        ("InvitacionAdministrador", "pei_invitacion_inmutable", "pei_invitacion_inmutable"),
        ("BootstrapPlataforma", "pei_bootstrap_no_update", "pei_proteger_modificacion_registro_email"),
        ("BootstrapPlataforma", "pei_bootstrap_no_truncate", "pei_proteger_eliminacion_registro_email"),
        ("AuditoriaPlataforma", "pei_auditoria_no_update", "pei_proteger_modificacion_registro_email"),
        ("AuditoriaPlataforma", "pei_auditoria_no_truncate", "pei_proteger_eliminacion_registro_email")
    ];
}
