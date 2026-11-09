using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using PEI.SalaEnVivo.Application.DTOs.Presencia;
using PEI.SalaEnVivo.Application.Interfaces;
using StackExchange.Redis;

namespace PEI.SalaEnVivo.Infrastructure.Services
{
    /// <summary>
    /// Presencia efímera de alumnos por sala. Usa Redis cuando está disponible y
    /// mantiene un fallback en memoria para desarrollo o caídas temporales.
    /// </summary>
    public sealed class PresenciaSalaRedisService : IPresenciaSalaService
    {
        private const string PrefijoAlias = "Alumno ";
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(4);

        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<PresenciaSalaRedisService>? _logger;
        private readonly ConcurrentDictionary<Guid, EstadoSalaMemoria> _memoria = new();

        public PresenciaSalaRedisService(
            ILogger<PresenciaSalaRedisService>? logger = null,
            IConnectionMultiplexer? redis = null)
        {
            _logger = logger;
            _redis = redis;
        }

        public async Task<RegistroPresenciaDto> RegistrarConexionAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId,
            CancellationToken cancellationToken = default)
        {
            if (UsarRedis)
            {
                try
                {
                    return await RegistrarEnRedisAsync(salaId, alumnoId, connectionId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "No se pudo registrar la presencia del alumno {AlumnoId} en la sala {SalaId} en Redis. Se usa el fallback en memoria.",
                        alumnoId,
                        salaId);
                }
            }

            return RegistrarEnMemoria(salaId, alumnoId, connectionId);
        }

        public async Task<ResultadoDesconexionPresenciaDto> EliminarConexionAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId,
            CancellationToken cancellationToken = default)
        {
            if (UsarRedis)
            {
                try
                {
                    return await EliminarEnRedisAsync(salaId, alumnoId, connectionId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "No se pudo eliminar la presencia del alumno {AlumnoId} en la sala {SalaId} de Redis. Se usa el fallback en memoria.",
                        alumnoId,
                        salaId);
                }
            }

            return EliminarEnMemoria(salaId, alumnoId, connectionId);
        }

        public async Task<PresenciaSalaDto> ObtenerPresenciaAsync(
            Guid salaId,
            CancellationToken cancellationToken = default)
        {
            if (UsarRedis)
            {
                try
                {
                    return await ObtenerPresenciaEnRedisAsync(salaId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "No se pudo leer la presencia de la sala {SalaId} desde Redis. Se usa el fallback en memoria.",
                        salaId);
                }
            }

            return ObtenerPresenciaEnMemoria(salaId);
        }

        public async Task<bool> EsConexionActivaAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId,
            CancellationToken cancellationToken = default)
        {
            if (UsarRedis)
            {
                try
                {
                    var valor = await _redis!.GetDatabase().HashGetAsync(
                        ClaveActiva(salaId),
                        alumnoId.ToString());

                    // Sin registro previo no bloqueamos el envío: puede ser una sala
                    // creada antes de habilitar la presencia o un fallback sin datos.
                    if (!valor.HasValue)
                    {
                        return true;
                    }

                    return string.Equals(valor.ToString(), connectionId, StringComparison.Ordinal);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(
                        ex,
                        "No se pudo verificar la sesión activa del alumno {AlumnoId} en la sala {SalaId} en Redis.",
                        alumnoId,
                        salaId);
                }
            }

            var estado = ObtenerEstadoMemoria(salaId);
            lock (estado.Sync)
            {
                return !estado.Activa.TryGetValue(alumnoId, out var activa)
                       || string.Equals(activa, connectionId, StringComparison.Ordinal);
            }
        }

        private bool UsarRedis => _redis is not null && _redis.IsConnected;

        private async Task<RegistroPresenciaDto> RegistrarEnRedisAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId)
        {
            var db = _redis!.GetDatabase();
            var ahora = DateTime.UtcNow;

            var resultado = await db.ScriptEvaluateAsync(
                ScriptRegistrar,
                new[]
                {
                    ClavePresencia(salaId),
                    ClaveConexiones(salaId),
                    ClaveActiva(salaId),
                    ClaveAlias(salaId),
                    ClaveContador(salaId)
                },
                new RedisValue[]
                {
                    alumnoId.ToString(),
                    connectionId,
                    ahora.ToString("O", CultureInfo.InvariantCulture),
                    (long)Ttl.TotalSeconds,
                    PrefijoAlias
                });

            var valores = (RedisResult[]?)resultado;
            if (valores is null || valores.Length < 5)
            {
                throw new InvalidOperationException(
                    "Redis devolvió una respuesta inesperada al registrar la presencia.");
            }

            var alias = valores[0].ToString();
            var ingresoUtc = ParsearIngreso(valores[1].ToString());
            var conexionAnterior = valores[2].ToString();
            var esNuevo = (long)valores[3] == 1;
            var totalConectados = (int)(long)valores[4];

            return new RegistroPresenciaDto(
                new AlumnoConectadoDto(alumnoId, alias, ingresoUtc),
                string.IsNullOrWhiteSpace(conexionAnterior) ? null : conexionAnterior,
                esNuevo,
                totalConectados);
        }

        private async Task<ResultadoDesconexionPresenciaDto> EliminarEnRedisAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId)
        {
            var db = _redis!.GetDatabase();

            var resultado = await db.ScriptEvaluateAsync(
                ScriptEliminar,
                new[]
                {
                    ClavePresencia(salaId),
                    ClaveConexiones(salaId),
                    ClaveActiva(salaId)
                },
                new RedisValue[]
                {
                    alumnoId.ToString(),
                    connectionId
                });

            var valores = (RedisResult[]?)resultado;
            if (valores is null || valores.Length < 4)
            {
                throw new InvalidOperationException(
                    "Redis devolvió una respuesta inesperada al eliminar la presencia.");
            }

            var eliminado = (long)valores[0] == 1;
            var totalConectados = (int)(long)valores[1];
            var alias = valores[2].ToString();
            var ingresoUtc = ParsearIngreso(valores[3].ToString());

            AlumnoConectadoDto? alumno = eliminado
                ? new AlumnoConectadoDto(alumnoId, alias, ingresoUtc)
                : null;

            return new ResultadoDesconexionPresenciaDto(eliminado, alumno, totalConectados);
        }

        private async Task<PresenciaSalaDto> ObtenerPresenciaEnRedisAsync(Guid salaId)
        {
            var db = _redis!.GetDatabase();
            var entradas = await db.HashGetAllAsync(ClavePresencia(salaId));

            var alumnos = entradas
                .Select(entrada =>
                {
                    var valor = entrada.Value.ToString();
                    var (alias, ingresoUtc) = SepararAliasEIngreso(valor);

                    return Guid.TryParse(entrada.Name.ToString(), out var alumnoId)
                        ? new AlumnoConectadoDto(alumnoId, alias, ingresoUtc)
                        : null;
                })
                .Where(alumno => alumno is not null)
                .Select(alumno => alumno!)
                .OrderBy(alumno => alumno.IngresoUtc)
                .ToArray();

            await RenovarTtlAsync(db, salaId);

            return new PresenciaSalaDto(salaId, alumnos.Length, alumnos, DateTime.UtcNow);
        }

        private static async Task RenovarTtlAsync(IDatabase db, Guid salaId)
        {
            // Un solo EVAL en lugar de cinco comandos: mismo efecto, un viaje de red.
            await db.ScriptEvaluateAsync(
                ScriptRenovarTtl,
                new[]
                {
                    ClavePresencia(salaId),
                    ClaveConexiones(salaId),
                    ClaveActiva(salaId),
                    ClaveAlias(salaId),
                    ClaveContador(salaId)
                },
                new RedisValue[] { (long)Ttl.TotalSeconds });
        }

        private RegistroPresenciaDto RegistrarEnMemoria(
            Guid salaId,
            Guid alumnoId,
            string connectionId)
        {
            var estado = ObtenerEstadoMemoria(salaId);
            var ahora = DateTime.UtcNow;

            lock (estado.Sync)
            {
                estado.Activa.TryGetValue(alumnoId, out var conexionAnterior);

                if (!string.IsNullOrWhiteSpace(conexionAnterior)
                    && !string.Equals(conexionAnterior, connectionId, StringComparison.Ordinal))
                {
                    estado.Conexiones.Remove(conexionAnterior);
                }

                estado.Activa[alumnoId] = connectionId;
                estado.Conexiones[connectionId] = alumnoId;

                if (!estado.AliasPorAlumno.TryGetValue(alumnoId, out var alias))
                {
                    alias = $"{PrefijoAlias}{++estado.ContadorAlias}";
                    estado.AliasPorAlumno[alumnoId] = alias;
                }

                var esNuevo = !estado.Alumnos.TryGetValue(alumnoId, out var alumno);
                if (alumno is null)
                {
                    alumno = new AlumnoConectadoDto(alumnoId, alias, ahora);
                    estado.Alumnos[alumnoId] = alumno;
                }

                return new RegistroPresenciaDto(
                    alumno with { Alias = alias },
                    conexionAnterior,
                    esNuevo,
                    estado.Alumnos.Count);
            }
        }

        private ResultadoDesconexionPresenciaDto EliminarEnMemoria(
            Guid salaId,
            Guid alumnoId,
            string connectionId)
        {
            var estado = ObtenerEstadoMemoria(salaId);

            lock (estado.Sync)
            {
                if (!estado.Activa.TryGetValue(alumnoId, out var activa)
                    || !string.Equals(activa, connectionId, StringComparison.Ordinal))
                {
                    return new ResultadoDesconexionPresenciaDto(
                        false,
                        null,
                        estado.Alumnos.Count);
                }

                estado.Activa.Remove(alumnoId);
                estado.Conexiones.Remove(connectionId);

                estado.Alumnos.TryGetValue(alumnoId, out var alumno);
                estado.Alumnos.Remove(alumnoId);

                return new ResultadoDesconexionPresenciaDto(
                    alumno is not null,
                    alumno,
                    estado.Alumnos.Count);
            }
        }

        private PresenciaSalaDto ObtenerPresenciaEnMemoria(Guid salaId)
        {
            var estado = ObtenerEstadoMemoria(salaId);

            lock (estado.Sync)
            {
                var alumnos = estado.Alumnos.Values
                    .OrderBy(alumno => alumno.IngresoUtc)
                    .ToArray();

                return new PresenciaSalaDto(
                    salaId,
                    alumnos.Length,
                    alumnos,
                    DateTime.UtcNow);
            }
        }

        private EstadoSalaMemoria ObtenerEstadoMemoria(Guid salaId)
        {
            return _memoria.GetOrAdd(salaId, _ => new EstadoSalaMemoria());
        }

        private static DateTime ParsearIngreso(string? valor)
        {
            if (DateTime.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var fecha))
            {
                return fecha.Kind == DateTimeKind.Utc ? fecha : fecha.ToUniversalTime();
            }

            return DateTime.UtcNow;
        }

        private static (string Alias, DateTime IngresoUtc) SepararAliasEIngreso(string valor)
        {
            var separador = valor.IndexOf('|', StringComparison.Ordinal);
            if (separador < 0)
            {
                return (valor, DateTime.UtcNow);
            }

            var alias = valor[..separador];
            var ingreso = ParsearIngreso(valor[(separador + 1)..]);

            return (alias, ingreso);
        }

        private static RedisKey ClavePresencia(Guid salaId) =>
            $"sala:{salaId}:presencia";

        private static RedisKey ClaveConexiones(Guid salaId) =>
            $"sala:{salaId}:conexiones";

        private static RedisKey ClaveActiva(Guid salaId) =>
            $"sala:{salaId}:conexion-activa";

        private static RedisKey ClaveAlias(Guid salaId) =>
            $"sala:{salaId}:alias-asignado";

        private static RedisKey ClaveContador(Guid salaId) =>
            $"sala:{salaId}:alias-contador";

        private sealed class EstadoSalaMemoria
        {
            public object Sync { get; } = new();
            public Dictionary<Guid, AlumnoConectadoDto> Alumnos { get; } = new();
            public Dictionary<string, Guid> Conexiones { get; } = new(StringComparer.Ordinal);
            public Dictionary<Guid, string> Activa { get; } = new();
            public Dictionary<Guid, string> AliasPorAlumno { get; } = new();
            public int ContadorAlias { get; set; }
        }

        private const string ScriptRegistrar = """
            local anterior = redis.call('HGET', KEYS[3], ARGV[1])
            if anterior and anterior ~= ARGV[2] then
              redis.call('HDEL', KEYS[2], anterior)
            end
            redis.call('HSET', KEYS[3], ARGV[1], ARGV[2])
            redis.call('HSET', KEYS[2], ARGV[2], ARGV[1])

            local alias = redis.call('HGET', KEYS[4], ARGV[1])
            if not alias then
              local n = redis.call('INCR', KEYS[5])
              alias = ARGV[5] .. n
              redis.call('HSET', KEYS[4], ARGV[1], alias)
            end

            local existente = redis.call('HGET', KEYS[1], ARGV[1])
            local ingreso = ARGV[3]
            if existente then
              local sep = string.find(existente, '|', 1, true)
              if sep then
                ingreso = string.sub(existente, sep + 1)
              end
            end

            redis.call('HSET', KEYS[1], ARGV[1], alias .. '|' .. ingreso)
            redis.call('EXPIRE', KEYS[1], ARGV[4])
            redis.call('EXPIRE', KEYS[2], ARGV[4])
            redis.call('EXPIRE', KEYS[3], ARGV[4])
            redis.call('EXPIRE', KEYS[4], ARGV[4])
            redis.call('EXPIRE', KEYS[5], ARGV[4])

            local esNuevo = 0
            if not existente then
              esNuevo = 1
            end

            return { alias, ingreso, anterior or '', esNuevo, redis.call('HLEN', KEYS[1]) }
            """;

        private const string ScriptEliminar = """
            local activa = redis.call('HGET', KEYS[3], ARGV[1])
            if not activa or activa ~= ARGV[2] then
              return { 0, redis.call('HLEN', KEYS[1]), '', '' }
            end

            local data = redis.call('HGET', KEYS[1], ARGV[1])
            redis.call('HDEL', KEYS[3], ARGV[1])
            redis.call('HDEL', KEYS[2], ARGV[2])

            if data then
              redis.call('HDEL', KEYS[1], ARGV[1])
            end

            local alias = ''
            local ingreso = ''
            if data then
              local sep = string.find(data, '|', 1, true)
              if sep then
                alias = string.sub(data, 1, sep - 1)
                ingreso = string.sub(data, sep + 1)
              end
            end

            return { 1, redis.call('HLEN', KEYS[1]), alias, ingreso }
            """;

        private const string ScriptRenovarTtl = """
            for i = 1, #KEYS do
              redis.call('EXPIRE', KEYS[i], ARGV[1])
            end

            return #KEYS
            """;
    }
}
