using System;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.Domain.Tests.Entities
{
    public class RespuestaSalaTests
    {
        [Fact]
        public void Crear_ParametrosValidos_CreaEntidadCorrectamente()
        {
            var institucionId = Guid.NewGuid();
            var salaId = Guid.NewGuid();
            var alumnoId = Guid.NewGuid();
            var preguntaId = Guid.NewGuid();
            var idempotencyKey = Guid.NewGuid();
            var respuestaTexto = "  Esta es una respuesta válida.  ";
            var secuencia = 1;
            var ahora = DateTime.UtcNow;

            var entidad = RespuestaSala.Crear(
                institucionId,
                salaId,
                alumnoId,
                preguntaId,
                respuestaTexto,
                idempotencyKey,
                secuencia,
                ahora);

            Assert.NotEqual(Guid.Empty, entidad.Id);
            Assert.Equal(institucionId, entidad.InstitucionId);
            Assert.Equal(salaId, entidad.SalaId);
            Assert.Equal(alumnoId, entidad.AlumnoId);
            Assert.Equal(preguntaId, entidad.PreguntaId);
            Assert.Equal("Esta es una respuesta válida.", entidad.Respuesta);
            Assert.Equal(idempotencyKey, entidad.IdempotencyKey);
            Assert.Equal(secuencia, entidad.Secuencia);
            Assert.Equal(ahora, entidad.RecibidaUtc);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Crear_RespuestaVaciaOEspacios_LanzaArgumentException(string? respuestaInvalida)
        {
            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    respuestaInvalida!,
                    Guid.NewGuid(),
                    1));
        }

        [Fact]
        public void Crear_RespuestaExcede10000Caracteres_LanzaArgumentException()
        {
            var respuestaLarga = new string('a', 10001);

            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    respuestaLarga,
                    Guid.NewGuid(),
                    1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Crear_SecuenciaInvalida_LanzaArgumentOutOfRangeException(int secuenciaInvalida)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                RespuestaSala.Crear(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Respuesta válida",
                    Guid.NewGuid(),
                    secuenciaInvalida));
        }

        [Fact]
        public void Crear_GuidVacio_LanzaArgumentException()
        {
            var valido = Guid.NewGuid();

            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(Guid.Empty, valido, valido, valido, "Respuesta", valido, 1));

            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(valido, Guid.Empty, valido, valido, "Respuesta", valido, 1));

            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(valido, valido, Guid.Empty, valido, "Respuesta", valido, 1));

            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(valido, valido, valido, Guid.Empty, "Respuesta", valido, 1));

            Assert.Throws<ArgumentException>(() =>
                RespuestaSala.Crear(valido, valido, valido, valido, "Respuesta", Guid.Empty, 1));
        }
    }
}
