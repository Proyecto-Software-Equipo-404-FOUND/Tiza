import type { ConexionSala } from '@/shared/realtime/tipos';
import type { EnvioPendiente, ResultadoEnvio } from './tipos';

export const MAX_INTENTOS = 3;

// Rechazos que el servidor marca como reintentables pero que no se arreglan repitiendo el mismo envío:
// con la misma secuencia vuelven a fallar. Se resuelven sincronizando la sala (ver useSalaAlumno).
const SIN_REINTENTO_AUTOMATICO = new Set(['SECUENCIA_FUERA_DE_ORDEN']);

export const esperarReal = (ms: number) => new Promise<void>((resolver) => setTimeout(resolver, ms));

// 1 s, 2 s, 4 s… más hasta 0,5 s de variación (sección 8.5 del contrato)
export const espera = (intento: number) => 1000 * 2 ** (intento - 1) + Math.random() * 500;

/** UUID v4 para la idempotencyKey. Se genera UNA vez por respuesta lógica. */
export function nuevaClave(): string {
  const nativa = globalThis.crypto?.randomUUID?.();
  if (nativa) return nativa;
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const n = Math.floor(Math.random() * 16);
    return (c === 'x' ? n : (n & 0x3) | 0x8).toString(16);
  });
}

/**
 * Envía una respuesta con el algoritmo de la sección 7.4 del contrato:
 * ante un error de red o un rechazo reintentable, se reintenta con la MISMA clave y secuencia.
 * Un rechazo no reintentable se devuelve tal cual; si se agotan los intentos por red, lanza.
 */
export async function enviarConReintentos(
  conexion: ConexionSala,
  envio: EnvioPendiente,
  esperar: (ms: number) => Promise<void> = esperarReal,
): Promise<ResultadoEnvio> {
  for (let intento = 1; ; intento++) {
    try {
      const resultado = (await conexion.invocar('EnviarRespuesta', {
        preguntaId: envio.preguntaId,
        respuesta: envio.respuesta,
        idempotencyKey: envio.idempotencyKey,
        secuencia: envio.secuencia,
      })) as ResultadoEnvio;

      const reintentable =
        resultado.estado === 'Rechazada' &&
        resultado.error?.reintentable &&
        !SIN_REINTENTO_AUTOMATICO.has(resultado.error.codigo);
      if (!reintentable || intento >= MAX_INTENTOS) return resultado;
    } catch (error) {
      // El servidor pudo haber aceptado el envío: se reintenta con la misma clave
      if (intento >= MAX_INTENTOS) throw error;
    }
    await esperar(espera(intento));
  }
}
