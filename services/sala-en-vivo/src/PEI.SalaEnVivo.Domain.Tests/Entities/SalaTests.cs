using System;
using PEI.SalaEnVivo.Domain.Entities;
using Xunit;

namespace PEI.SalaEnVivo.Domain.Tests.Entities
{
    public class SalaTests
    {
        // Test 1: Invalid capacity must be rejected.
        [Fact]
        public void ModificarCapacidadMaxima_CapacidadInvalida_RetornaFalse()
        {
            var sala = new Sala
            {
                CapacidadMaxima = 10,
                CantidadParticipantes = 5
            };

            var resultado = sala.ModificarCapacidadMaxima(4);

            Assert.False(resultado);
            Assert.Equal(10, sala.CapacidadMaxima);
        }

        // Test 2: Valid capacity must update the maximum capacity.
        [Fact]
        public void ModificarCapacidadMaxima_CapacidadValida_ActualizaCapacidad()
        {
            var sala = new Sala
            {
                CapacidadMaxima = 10,
                CantidadParticipantes = 5
            };

            var resultado = sala.ModificarCapacidadMaxima(8);

            Assert.True(resultado);
            Assert.Equal(8, sala.CapacidadMaxima);
        }

        // Test 3: SetCapacidadMaxima must throw an exception for invalid capacity.
        [Fact]
        public void SetCapacidadMaxima_CapacidadInvalida_LanzaExcepcion()
        {
            var sala = new Sala();

            Assert.Throws<ArgumentException>(
                () => sala.SetCapacidadMaxima(0));
        }

        // Test 4: A new room must have the expected initial values.
        [Fact]
        public void NuevaSala_TieneValoresInicialesEsperados()
        {
            var sala = new Sala();

            Assert.Equal(Guid.Empty, sala.Id);
            Assert.Empty(sala.Version);
            Assert.Equal(string.Empty, sala.Estado);
            Assert.Equal(0, sala.CapacidadMaxima);
            Assert.Equal(0, sala.CantidadParticipantes);
        }
    }
}
