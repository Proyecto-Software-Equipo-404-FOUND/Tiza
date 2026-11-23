using System;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.Domain.Tests.Entities
{
    // S3-SA-02 (#239): el reloj del examen arranca al iniciar la sala, no al crearla.
    public class SalaRelojTests
    {
        [Fact]
        public void Iniciar_PrimeraVez_FijaInicioYVencimientoConLaDuracion()
        {
            var inicio = new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);
            var sala = new Sala { Estado = "Activa", DuracionMinutos = 45, FechaVencimiento = inicio.AddHours(24) };

            sala.Iniciar(inicio);

            Assert.Equal("EnCurso", sala.Estado);
            Assert.Equal(inicio, sala.IniciadaUtc);
            Assert.Equal(inicio.AddMinutes(45), sala.FechaVencimiento);
            Assert.True(sala.FueIniciada);
        }

        [Fact]
        public void Iniciar_DosVeces_NoMueveElReloj()
        {
            var inicio = new DateTime(2026, 11, 20, 14, 0, 0, DateTimeKind.Utc);
            var sala = new Sala { Estado = "Activa", DuracionMinutos = 30 };
            sala.Iniciar(inicio);

            sala.Iniciar(inicio.AddMinutes(10));

            Assert.Equal(inicio, sala.IniciadaUtc);
            Assert.Equal(inicio.AddMinutes(30), sala.FechaVencimiento);
        }

        [Fact]
        public void SalaNueva_NoEstaIniciadaYUsaLaDuracionPorDefecto()
        {
            var sala = new Sala();

            Assert.False(sala.FueIniciada);
            Assert.Equal(Sala.DuracionPorDefectoMinutos, sala.DuracionMinutos);
        }
    }
}
