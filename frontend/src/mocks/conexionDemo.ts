import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import type { ConexionSala } from '@/shared/realtime/tipos';

const SALA_DEMO_ID = '11111111-1111-4111-8111-111111111111';
const esperar = (ms: number) => new Promise((resolver) => setTimeout(resolver, ms));

// Sala de mentira para la demo. PIN de prueba:
//   123456 entra y a los 8 segundos «el docente» inicia la sala
//   111111 sala llena · 222222 sala finalizada · 333333 sala inexistente (mensaje actual del hub)
//   444444 entra a una sala que ya está en curso
export function crearConexionDemo(): ConexionSala {
  const { conexion, simular } = crearConexionFalsa();

  return {
    ...conexion,
    invocar: async (metodo, ...argumentos) => {
      if (metodo !== 'UnirseASala') return;
      await esperar(400);
      const pin = Number(argumentos[0]);

      if (pin === 111111) throw new Error('SALA_LLENA: la sala alcanzó su capacidad máxima.');
      if (pin === 222222) throw new Error('SALA_FINALIZADA: la sala ya cerró.');
      if (pin === 333333) throw new Error('La sala no existe, está inactiva o venció.');

      if (pin === 444444) {
        simular('estadoSala', { salaId: SALA_DEMO_ID, estado: 'EnCurso', fechaLimiteUtc: null });
        return;
      }
      if (pin === 123456) {
        simular('estadoSala', { salaId: SALA_DEMO_ID, estado: 'Activa', fechaLimiteUtc: null });
        setTimeout(() => {
          const ahora = new Date();
          simular('salaIniciada', {
            salaId: SALA_DEMO_ID,
            iniciadaUtc: ahora.toISOString(),
            totalPreguntas: 2,
            fechaLimiteUtc: new Date(ahora.getTime() + 30 * 60 * 1000).toISOString(),
          });
        }, 8000);
        return;
      }
      throw new Error('SALA_NO_ENCONTRADA: el PIN no corresponde a ninguna sala.');
    },
  };
}
