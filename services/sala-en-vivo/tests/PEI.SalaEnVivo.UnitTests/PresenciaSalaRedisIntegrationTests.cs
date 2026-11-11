using PEI.SalaEnVivo.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    /// <summary>
    /// Integración real contra Redis local. A diferencia de
    /// <see cref="PresenciaSalaRedisServiceTests"/>, que ejercita el fallback en memoria,
    /// estos tests ejecutan los scripts Lua y validan las claves y el TTL.
    /// Si Redis no responde en localhost:6379, los tests se omiten.
    /// </summary>
    public class PresenciaSalaRedisIntegrationTests
    {
        private static IConnectionMultiplexer Redis =>
            RedisLocal.Conexion
            ?? throw new InvalidOperationException("Redis no disponible en localhost:6379.");

        private static PresenciaSalaRedisService CrearServicio() =>
            new PresenciaSalaRedisService(redis: Redis);

        private static string[] Claves(Guid salaId) => new[]
        {
            $"sala:{salaId}:presencia",
            $"sala:{salaId}:conexiones",
            $"sala:{salaId}:conexion-activa",
            $"sala:{salaId}:alias-asignado",
            $"sala:{salaId}:alias-contador"
        };

        private static void Limpiar(Guid salaId)
        {
            var db = Redis.GetDatabase();

            foreach (var clave in Claves(salaId))
            {
                db.KeyDelete(clave);
            }
        }

        [RedisFact]
        public async Task RegistrarConexion_EscribePresenciaEnRedisConTtl()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();

            try
            {
                var servicio = CrearServicio();

                var registro = await servicio.RegistrarConexionAsync(
                    salaId,
                    alumnoId,
                    "conexion-1");

                Assert.True(registro.EsNuevo);
                Assert.Equal("Alumno 1", registro.Alumno.Alias);
                Assert.Equal(1, registro.TotalConectados);

                var db = Redis.GetDatabase();

                var presencia = await db.HashGetAsync(
                    $"sala:{salaId}:presencia",
                    alumnoId.ToString());

                Assert.True(presencia.HasValue);
                Assert.StartsWith("Alumno 1|", presencia.ToString());

                var alias = await db.HashGetAsync(
                    $"sala:{salaId}:alias-asignado",
                    alumnoId.ToString());

                Assert.Equal("Alumno 1", alias.ToString());

                var ttl = await db.KeyTimeToLiveAsync($"sala:{salaId}:presencia");

                Assert.NotNull(ttl);
                Assert.InRange(ttl!.Value.TotalMinutes, 230, 241);
            }
            finally
            {
                Limpiar(salaId);
            }
        }

        [RedisFact]
        public async Task ReconectarConOtroAlumnoConectado_NoInflaElTotalNiDuplica()
        {
            var salaId = Guid.NewGuid();
            var alumno1 = Guid.NewGuid();
            var alumno2 = Guid.NewGuid();

            try
            {
                var servicio = CrearServicio();

                await servicio.RegistrarConexionAsync(salaId, alumno1, "conexion-1");
                await servicio.RegistrarConexionAsync(salaId, alumno2, "conexion-2");

                // Alumno 1 reconecta mientras Alumno 2 sigue conectado.
                var reconexion = await servicio.RegistrarConexionAsync(
                    salaId,
                    alumno1,
                    "conexion-1b");

                var presencia = await servicio.ObtenerPresenciaAsync(salaId);

                Assert.False(reconexion.EsNuevo);
                Assert.Equal("conexion-1", reconexion.ConexionAnterior);
                Assert.Equal("Alumno 1", reconexion.Alumno.Alias);

                // Lo central: sigue habiendo 2 alumnos, no 3 ni un duplicado.
                Assert.Equal(2, presencia.TotalConectados);
                Assert.Equal(2, presencia.Alumnos.Count);
                Assert.Single(presencia.Alumnos, alumno => alumno.AlumnoId == alumno1);
                Assert.Equal(
                    2,
                    presencia.Alumnos.Select(alumno => alumno.AlumnoId).Distinct().Count());

                var db = Redis.GetDatabase();

                Assert.Equal(
                    "conexion-1b",
                    (await db.HashGetAsync(
                        $"sala:{salaId}:conexion-activa",
                        alumno1.ToString())).ToString());

                Assert.False(await db.HashExistsAsync(
                    $"sala:{salaId}:conexiones",
                    "conexion-1"));

                // El contador de alias no se incrementa al reconectar.
                Assert.Equal(
                    "2",
                    (await db.StringGetAsync($"sala:{salaId}:alias-contador")).ToString());
            }
            finally
            {
                Limpiar(salaId);
            }
        }

        [RedisFact]
        public async Task CerrarConexionReemplazada_NoEliminaLaPresencia()
        {
            var salaId = Guid.NewGuid();
            var alumno1 = Guid.NewGuid();
            var alumno2 = Guid.NewGuid();

            try
            {
                var servicio = CrearServicio();

                await servicio.RegistrarConexionAsync(salaId, alumno1, "conexion-1");
                await servicio.RegistrarConexionAsync(salaId, alumno2, "conexion-2");
                await servicio.RegistrarConexionAsync(salaId, alumno1, "conexion-1b");

                // La conexión vieja se cierra después de haber sido reemplazada.
                var resultado = await servicio.EliminarConexionAsync(
                    salaId,
                    alumno1,
                    "conexion-1");

                var presencia = await servicio.ObtenerPresenciaAsync(salaId);

                Assert.False(resultado.AlumnoEliminado);
                Assert.Null(resultado.Alumno);
                Assert.Equal(2, presencia.TotalConectados);
                Assert.Single(presencia.Alumnos, alumno => alumno.AlumnoId == alumno1);

                Assert.True(await servicio.EsConexionActivaAsync(
                    salaId,
                    alumno1,
                    "conexion-1b"));

                Assert.False(await servicio.EsConexionActivaAsync(
                    salaId,
                    alumno1,
                    "conexion-1"));
            }
            finally
            {
                Limpiar(salaId);
            }
        }

        [RedisFact]
        public async Task CerrarConexionActiva_EliminaAlAlumnoPeroConservaElAlias()
        {
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();

            try
            {
                var servicio = CrearServicio();

                var registro = await servicio.RegistrarConexionAsync(
                    salaId,
                    alumnoId,
                    "conexion-1");

                var resultado = await servicio.EliminarConexionAsync(
                    salaId,
                    alumnoId,
                    "conexion-1");

                var presencia = await servicio.ObtenerPresenciaAsync(salaId);

                Assert.True(resultado.AlumnoEliminado);
                Assert.NotNull(resultado.Alumno);
                Assert.Equal(registro.Alumno.Alias, resultado.Alumno!.Alias);
                Assert.Equal(0, presencia.TotalConectados);
                Assert.Empty(presencia.Alumnos);

                var db = Redis.GetDatabase();

                // Redis elimina el hash vacío automáticamente.
                Assert.False(await db.KeyExistsAsync($"sala:{salaId}:presencia"));

                // El alias sobrevive para el reingreso del mismo alumno.
                Assert.True(await db.HashExistsAsync(
                    $"sala:{salaId}:alias-asignado",
                    alumnoId.ToString()));
            }
            finally
            {
                Limpiar(salaId);
            }
        }
    }

    /// <summary>
    /// Fact que se omite cuando Redis no está disponible, para que el proyecto de tests
    /// siga corriendo en entornos sin el contenedor levantado.
    /// </summary>
    public sealed class RedisFactAttribute : FactAttribute
    {
        public RedisFactAttribute()
        {
            if (!RedisLocal.Disponible)
            {
                Skip = "Redis no disponible en localhost:6379.";
            }
        }
    }

    internal static class RedisLocal
    {
        private static readonly Lazy<IConnectionMultiplexer?> ConexionPerezosa = new(
            () =>
            {
                try
                {
                    var config = ConfigurationOptions.Parse("localhost:6379");
                    config.AbortOnConnectFail = true;
                    config.ConnectTimeout = 1000;
                    config.ConnectRetry = 1;
                    config.SyncTimeout = 1000;

                    var multiplexer = ConnectionMultiplexer.Connect(config);

                    return multiplexer.IsConnected ? multiplexer : null;
                }
                catch
                {
                    return null;
                }
            });

        public static IConnectionMultiplexer? Conexion => ConexionPerezosa.Value;

        public static bool Disponible => ConexionPerezosa.Value is not null;
    }
}
