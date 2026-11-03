import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import type { ConexionSala } from '@/shared/realtime/tipos';
import type { EnvioPendiente, PreguntaActual, ResultadoEnvio } from '@/features/sala-alumno/tipos';

const SALA_DEMO_ID = '11111111-1111-4111-8111-111111111111';
const P1_ID = '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60';
const P2_ID = '9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f';
const OPCION_CORRECTA = '7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d';
const OPCION_OTRA = '8b2c3d4e-5f60-4b7c-9d8e-0f1a2b3c4d5e';

const esperar = (ms: number) => new Promise((resolver) => setTimeout(resolver, ms));
const enSegundos = (segundos: number) => new Date(Date.now() + segundos * 1000).toISOString();

const pregunta1 = (): PreguntaActual => ({
  salaId: SALA_DEMO_ID,
  preguntaId: P1_ID,
  indice: 1,
  totalPreguntas: 2,
  tipo: 'OpcionMultiple',
  enunciado: '¿Qué imprime Console.WriteLine(2 + 3)?',
  opciones: [
    { id: OPCION_CORRECTA, texto: '5' },
    { id: OPCION_OTRA, texto: '23' },
  ],
  puntajeMaximo: 2,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(120),
});

const pregunta2 = (): PreguntaActual => ({
  salaId: SALA_DEMO_ID,
  preguntaId: P2_ID,
  indice: 2,
  totalPreguntas: 2,
  tipo: 'Abierta',
  enunciado: 'Explicá qué es una variable.',
  opciones: null,
  puntajeMaximo: 4,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(180),
});

// Sala de mentira para la demo. PIN de prueba:
//   123456 entra, a los 8 s «el docente» inicia y el alumno recorre las 2 preguntas
//   444444 entra a una sala ya en curso (primera pregunta)
//   555555 entra a mitad del examen: ya respondió la 1 y va por la 2 (recuperación de estado)
//   111111 sala llena · 222222 sala finalizada · 333333 sala inexistente
// En la respuesta abierta: «rechazar» simula una respuesta inválida y «red» simula un
// corte de red en el primer intento (se reintenta con la misma clave y se acepta).
export function crearConexionDemo(): ConexionSala {
  const { conexion, simular } = crearConexionFalsa();
  let ultimaSecuencia = 0;
  const respondidas = new Set<string>();
  const porClave = new Map<string, { envio: EnvioPendiente; recibidaUtc: string }>();
  const intentosPorClave = new Map<string, number>();

  const resultado = (envio: EnvioPendiente, estado: ResultadoEnvio['estado'], recibidaUtc: string, error: ResultadoEnvio['error'] = null): ResultadoEnvio => ({
    estado,
    preguntaId: envio.preguntaId,
    idempotencyKey: envio.idempotencyKey,
    secuencia: envio.secuencia,
    recibidaUtc,
    error,
  });

  const enviarRespuesta = async (envio: EnvioPendiente): Promise<ResultadoEnvio> => {
    await esperar(300);

    // 1) idempotencia: un reintento de un envío ya aceptado devuelve Duplicada
    const previo = porClave.get(envio.idempotencyKey);
    if (previo) return resultado(envio, 'Duplicada', previo.recibidaUtc);

    // Corte de red simulado: el servidor nunca recibe el primer intento
    const intentos = (intentosPorClave.get(envio.idempotencyKey) ?? 0) + 1;
    intentosPorClave.set(envio.idempotencyKey, intentos);
    if (envio.respuesta.toLowerCase() === 'red' && intentos === 1) throw new Error('Failed to fetch');

    const ahora = new Date().toISOString();
    const rechazo = (codigo: string, mensaje: string) =>
      resultado(envio, 'Rechazada', ahora, { codigo, mensaje, reintentable: false });

    if (envio.respuesta.toLowerCase() === 'rechazar') {
      return rechazo('RESPUESTA_INVALIDA', 'La respuesta no es válida.');
    }
    if (respondidas.has(envio.preguntaId)) {
      return rechazo('RESPUESTA_YA_REGISTRADA', 'Ya respondiste esta pregunta.');
    }
    if (envio.secuencia !== ultimaSecuencia + 1) {
      return rechazo('SECUENCIA_FUERA_DE_ORDEN', 'La secuencia no es la esperada.');
    }

    ultimaSecuencia = envio.secuencia;
    respondidas.add(envio.preguntaId);
    porClave.set(envio.idempotencyKey, { envio, recibidaUtc: ahora });

    // «El docente» avanza solo, para poder ver todo el recorrido
    if (envio.preguntaId === P1_ID) setTimeout(() => simular('preguntaActual', pregunta2()), 2500);
    if (envio.preguntaId === P2_ID) {
      setTimeout(
        () =>
          simular('salaFinalizada', {
            salaId: SALA_DEMO_ID,
            finalizadaUtc: new Date().toISOString(),
            motivo: 'Docente',
          }),
        2500,
      );
    }
    return resultado(envio, 'Aceptada', ahora);
  };

  return {
    ...conexion,
    invocar: async (metodo, ...argumentos) => {
      if (metodo === 'EnviarRespuesta') return enviarRespuesta(argumentos[0] as EnvioPendiente);
      if (metodo !== 'UnirseASala') return undefined;

      await esperar(400);
      const pin = Number(argumentos[0]);

      if (pin === 111111) throw new Error('SALA_LLENA: la sala alcanzó su capacidad máxima.');
      if (pin === 222222) throw new Error('SALA_FINALIZADA: la sala ya cerró.');
      if (pin === 333333) throw new Error('La sala no existe, está inactiva o venció.');

      if (pin === 444444) {
        simular('estadoSala', {
          salaId: SALA_DEMO_ID,
          estado: 'EnCurso',
          fechaLimiteUtc: enSegundos(1800),
          preguntaActual: pregunta1(),
          ultimaSecuencia: 0,
          preguntasRespondidas: [],
          servidorUtc: new Date().toISOString(),
        });
        return undefined;
      }
      if (pin === 555555) {
        ultimaSecuencia = 1;
        respondidas.add(P1_ID);
        simular('estadoSala', {
          salaId: SALA_DEMO_ID,
          estado: 'EnCurso',
          fechaLimiteUtc: enSegundos(1800),
          preguntaActual: pregunta2(),
          ultimaSecuencia: 1,
          preguntasRespondidas: [P1_ID],
          servidorUtc: new Date().toISOString(),
        });
        return undefined;
      }
      if (pin === 123456) {
        simular('estadoSala', { salaId: SALA_DEMO_ID, estado: 'Activa', fechaLimiteUtc: null });
        setTimeout(() => {
          simular('salaIniciada', {
            salaId: SALA_DEMO_ID,
            iniciadaUtc: new Date().toISOString(),
            totalPreguntas: 2,
            fechaLimiteUtc: enSegundos(1800),
          });
          simular('preguntaActual', pregunta1());
        }, 8000);
        return undefined;
      }
      throw new Error('SALA_NO_ENCONTRADA: el PIN no corresponde a ninguna sala.');
    },
  };
}
