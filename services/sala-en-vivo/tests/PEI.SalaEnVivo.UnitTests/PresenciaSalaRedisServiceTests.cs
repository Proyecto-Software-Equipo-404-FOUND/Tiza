using PEI.SalaEnVivo.Infrastructure.Services;
using Xunit;

namespace PEI.SalaEnVivo.UnitTests
{
    public class PresenciaSalaRedisServiceTests
    {
        private static PresenciaSalaRedisService CrearServicio()
        {
            // Sin multiplexer se ejercita el fallback en memoria, que replica la
            // semántica de presencia usada cuando Redis no está disponible.
            return new PresenciaSalaRedisService(redis: null);
        }

        [Fact]
        public async Task RegistrarConexion_AsignaAliasYCuentaAlumnos()
        {
            var servicio = CrearServicio();
            var salaId = Guid.NewGuid();
            var alumno1 = Guid.NewGuid();
            var alumno2 = Guid.NewGuid();

            var primero = await servicio.RegistrarConexionAsync(salaId, alumno1, "conexion-1");
            var segundo = await servicio.RegistrarConexionAsync(salaId, alumno2, "conexion-2");
            var presencia = await servicio.ObtenerPresenciaAsync(salaId);

            Assert.True(primero.EsNuevo);
            Assert.Equal("Alumno 1", primero.Alumno.Alias);
            Assert.Equal(1, primero.TotalConectados);

            Assert.True(segundo.EsNuevo);
            Assert.Equal("Alumno 2", segundo.Alumno.Alias);
            Assert.Equal(2, segundo.TotalConectados);

            Assert.Equal(2, presencia.TotalConectados);
            Assert.Equal(2, presencia.Alumnos.Count);
            Assert.Equal(
                new[] { alumno1, alumno2 },
                presencia.Alumnos.Select(alumno => alumno.AlumnoId).ToArray());
        }

        [Fact]
        public async Task RegistrarConexion_ReconexionDelMismoAlumno_MantieneAliasEIngreso()
        {
            var servicio = CrearServicio();
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();

            var original = await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-vieja");
            var reconexion = await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-nueva");
            var presencia = await servicio.ObtenerPresenciaAsync(salaId);

            Assert.False(reconexion.EsNuevo);
            Assert.Equal("conexion-vieja", reconexion.ConexionAnterior);
            Assert.Equal(original.Alumno.Alias, reconexion.Alumno.Alias);
            Assert.Equal(original.Alumno.IngresoUtc, reconexion.Alumno.IngresoUtc);
            Assert.Equal(1, presencia.TotalConectados);
        }

        [Fact]
        public async Task EliminarConexion_ConexionReemplazada_NoEliminaLaPresencia()
        {
            var servicio = CrearServicio();
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();

            await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-vieja");
            await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-nueva");

            var resultadoViejo = await servicio.EliminarConexionAsync(salaId, alumnoId, "conexion-vieja");
            var presencia = await servicio.ObtenerPresenciaAsync(salaId);

            Assert.False(resultadoViejo.AlumnoEliminado);
            Assert.Equal(1, presencia.TotalConectados);

            var resultadoNuevo = await servicio.EliminarConexionAsync(salaId, alumnoId, "conexion-nueva");
            var presenciaFinal = await servicio.ObtenerPresenciaAsync(salaId);

            Assert.True(resultadoNuevo.AlumnoEliminado);
            Assert.NotNull(resultadoNuevo.Alumno);
            Assert.Equal(0, presenciaFinal.TotalConectados);
            Assert.Empty(presenciaFinal.Alumnos);
        }

        [Fact]
        public async Task EsConexionActiva_SoloLaUltimaConexionDelAlumno()
        {
            var servicio = CrearServicio();
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();

            await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-vieja");
            await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-nueva");

            Assert.False(await servicio.EsConexionActivaAsync(salaId, alumnoId, "conexion-vieja"));
            Assert.True(await servicio.EsConexionActivaAsync(salaId, alumnoId, "conexion-nueva"));
        }

        [Fact]
        public async Task EsConexionActiva_SinRegistroPrevio_NoBloquea()
        {
            var servicio = CrearServicio();

            Assert.True(await servicio.EsConexionActivaAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "conexion-sin-registro"));
        }

        [Fact]
        public async Task RegistrarConexion_TrasSalir_MantieneAliasAsignado()
        {
            var servicio = CrearServicio();
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();

            var original = await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-1");
            await servicio.EliminarConexionAsync(salaId, alumnoId, "conexion-1");

            var reingreso = await servicio.RegistrarConexionAsync(salaId, alumnoId, "conexion-2");

            Assert.True(reingreso.EsNuevo);
            Assert.Equal(original.Alumno.Alias, reingreso.Alumno.Alias);
        }
    }
}
