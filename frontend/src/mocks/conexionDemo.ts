import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import type { ConexionSala } from '@/shared/realtime/tipos';
import type { EnvioPendiente, PreguntaActual, ResultadoEnvio } from '@/features/sala-alumno/tipos';

const SALA_DEMO_ID = '11111111-1111-4111-8111-111111111111';
const P1_ID = '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60';
const P2_ID = '9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f';
const OPCION_CORRECTA = '7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d';
const OPCION_OTRA = '8b2c3d4e-5f60-4b7c-9d8e-0f1a2b3c4d5e';

/* IDs y constantes para la sala demo de Quiz gamificado (PIN 777777) */
const SALA_QUIZ_DEMO_ID = '77777777-7777-4777-8777-777777777777';
const Q1_ID = '7f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f01';
const Q2_ID = '7f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f02';
const Q3_ID = '7f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f03';
const Q4_ID = '7f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f04';
const Q5_ID = '7f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f05';

const esperar = (ms: number) => new Promise((resolver) => setTimeout(resolver, ms));
const enSegundos = (segundos: number) => new Date(Date.now() + segundos * 1000).toISOString();

const quizPregunta1 = (): PreguntaActual => ({
  salaId: SALA_QUIZ_DEMO_ID,
  preguntaId: Q1_ID,
  indice: 1,
  totalPreguntas: 5,
  tipo: 'BucketSort',
  enunciado: 'Clasificá cada protocolo según su capa en el modelo TCP/IP:',
  puntajeMaximo: 1.5,
  puntajePorItem: 0.3,
  opciones: null,
  cestas: [
    { id: 'capa_aplicacion', titulo: 'Capa de Aplicación', icono: '🌐' },
    { id: 'capa_red_transporte', titulo: 'Red y Transporte', icono: '🔀' },
  ],
  itemsBucket: [
    { id: 'it_http', texto: 'HTTP / HTTPS', cestaCorrecta: 'capa_aplicacion' },
    { id: 'it_ip', texto: 'Protocolo IP', cestaCorrecta: 'capa_red_transporte' },
    { id: 'it_dns', texto: 'DNS', cestaCorrecta: 'capa_aplicacion' },
    { id: 'it_tcp', texto: 'TCP / UDP', cestaCorrecta: 'capa_red_transporte' },
    { id: 'it_smtp', texto: 'SMTP (Correo)', cestaCorrecta: 'capa_aplicacion' },
  ],
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(180),
});

const quizPregunta2 = (): PreguntaActual => ({
  salaId: SALA_QUIZ_DEMO_ID,
  preguntaId: Q2_ID,
  indice: 2,
  totalPreguntas: 5,
  tipo: 'ClasificadorRapido',
  enunciado: 'Determiná si cada afirmación sobre telecomunicaciones y modulación es Verdadera o Falsa:',
  puntajeMaximo: 1.5,
  puntajePorItem: 0.3,
  opciones: null,
  afirmaciones: [
    { id: 'af_1', texto: 'La modulación AM varía la amplitud de la portadora manteniendo su frecuencia constante.', esVerdadera: true },
    { id: 'af_2', texto: 'En el modelo OSI, la dirección MAC opera en la Capa de Red (Capa 3).', esVerdadera: false },
    { id: 'af_3', texto: 'La fibra óptica transmite señales en el espectro de la luz visible o infrarroja.', esVerdadera: true },
    { id: 'af_4', texto: 'A mayor frecuencia de una onda electromagnética, mayor es su longitud de onda.', esVerdadera: false },
    { id: 'af_5', texto: 'El espectro electromagnético de radiofrecuencia es un recurso natural finito y regulado.', esVerdadera: true },
  ],
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(180),
});

const quizPregunta3 = (): PreguntaActual => ({
  salaId: SALA_QUIZ_DEMO_ID,
  preguntaId: Q3_ID,
  indice: 3,
  totalPreguntas: 5,
  tipo: 'Emparejamiento',
  enunciado: 'Asociá cada concepto de modulación y capa física con su definición correspondiente:',
  puntajeMaximo: 1.5,
  puntajePorItem: 0.3,
  opciones: null,
  pares: [
    { conceptoId: 'c1', conceptoTexto: 'Modulación QAM', definicionId: 'd1', definicionTexto: 'Combina variación de amplitud y fase de la portadora.' },
    { conceptoId: 'c2', conceptoTexto: 'Ancho de Banda', definicionId: 'd2', definicionTexto: 'Rango de frecuencias que puede transportar el canal.' },
    { conceptoId: 'c3', conceptoTexto: 'Capa Física', definicionId: 'd3', definicionTexto: 'Transmisión de cadenas de bits sobre el medio de transmisión.' },
    { conceptoId: 'c4', conceptoTexto: 'Modulación FM', definicionId: 'd4', definicionTexto: 'Varía la frecuencia de la portadora según la moduladora.' },
    { conceptoId: 'c5', conceptoTexto: 'Atenuación', definicionId: 'd5', definicionTexto: 'Pérdida paulatina de potencia de la señal con la distancia.' },
  ],
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(180),
});

const quizPregunta4 = (): PreguntaActual => ({
  salaId: SALA_QUIZ_DEMO_ID,
  preguntaId: Q4_ID,
  indice: 4,
  totalPreguntas: 5,
  tipo: 'Ordenamiento',
  enunciado: 'Ordená las capas del Modelo OSI desde la capa inferior (Capa 1) hacia la superior (Capa 4):',
  puntajeMaximo: 1,
  opciones: null,
  itemsOrden: [
    { id: 'l1', texto: 'Capa 1: Capa Física (señales eléctricas/ópticas y bits)', ordenCorrecto: 1 },
    { id: 'l2', texto: 'Capa 2: Capa de Enlace de Datos (tramas y direcciones MAC)', ordenCorrecto: 2 },
    { id: 'l3', texto: 'Capa 3: Capa de Red (paquetes y enrutamiento IP)', ordenCorrecto: 3 },
    { id: 'l4', texto: 'Capa 4: Capa de Transporte (segmentos, TCP/UDP)', ordenCorrecto: 4 },
  ],
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(180),
});

const quizPregunta5 = (): PreguntaActual => ({
  salaId: SALA_QUIZ_DEMO_ID,
  preguntaId: Q5_ID,
  indice: 5,
  totalPreguntas: 5,
  tipo: 'OpcionMultiple',
  enunciado: '¿Qué protocolo de la capa de transporte proporciona una entrega confiable y orientada a la conexión?',
  puntajeMaximo: 2,
  opciones: [
    { id: 'op_tcp', texto: 'TCP (Transmission Control Protocol)' },
    { id: 'op_udp', texto: 'UDP (User Datagram Protocol)' },
    { id: 'op_icmp', texto: 'ICMP (Internet Control Message Protocol)' },
  ],
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(120),
});

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
// «corte» simula que se cae la conexión durante 6 s: se ve «Reconectando…», el envío queda
// pendiente y, al volver, se recupera la sala y se reenvía con la misma clave.
export function crearConexionDemo(): ConexionSala {
  const { conexion, simular: emitir, simularEstado } = crearConexionFalsa();
  let ultimaSecuencia = 0;
  let cortada = false;
  // Lo que «sabe» el servidor de la sala, para poder devolverlo en ObtenerEstadoSala
  let estadoSala: 'Activa' | 'EnCurso' | 'Finalizada' = 'Activa';
  let vigente: PreguntaActual | null = null;
  const simular = (evento: string, carga: unknown) => {
    if (evento === 'salaIniciada') estadoSala = 'EnCurso';
    if (evento === 'preguntaActual') {
      estadoSala = 'EnCurso';
      vigente = carga as PreguntaActual;
    }
    if (evento === 'salaFinalizada') estadoSala = 'Finalizada';
    if (evento === 'estadoSala') {
      const e = carga as { estado: typeof estadoSala; preguntaActual?: PreguntaActual | null };
      estadoSala = e.estado;
      vigente = e.preguntaActual ?? null;
    }
    emitir(evento, carga);
  };
  const reportarEstado = () => {
    const carga = {
      salaId: SALA_DEMO_ID,
      estado: estadoSala,
      fechaLimiteUtc: enSegundos(1800),
      preguntaActual: estadoSala === 'EnCurso' ? vigente : null,
      ultimaSecuencia,
      preguntasRespondidas: [...respondidas],
      servidorUtc: new Date().toISOString(),
    };
    emitir('estadoSala', carga);
    return carga;
  };
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

    // Mientras la conexión está caída el envío no llega al servidor
    if (cortada) throw new Error('Failed to fetch');

    // 1) idempotencia: un reintento de un envío ya aceptado devuelve Duplicada
    const previo = porClave.get(envio.idempotencyKey);
    if (previo) return resultado(envio, 'Duplicada', previo.recibidaUtc);

    // Corte de red simulado: el servidor nunca recibe el primer intento
    const intentos = (intentosPorClave.get(envio.idempotencyKey) ?? 0) + 1;
    intentosPorClave.set(envio.idempotencyKey, intentos);
    if (envio.respuesta.toLowerCase() === 'red' && intentos === 1) throw new Error('Failed to fetch');

    // Corte de conexión simulado: se cae, y vuelve a los 6 s
    if (envio.respuesta.toLowerCase() === 'corte' && !porClave.has(envio.idempotencyKey) && !cortada && intentos === 1) {
      cortada = true;
      simularEstado('reconectando');
      setTimeout(() => {
        cortada = false;
        simularEstado('conectado');
      }, 6000);
      throw new Error('Failed to fetch');
    }

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

    /* Avance de preguntas en la sala de Quiz gamificado (PIN 777777) */
    if (envio.preguntaId === Q1_ID) setTimeout(() => simular('preguntaActual', quizPregunta2()), 2000);
    if (envio.preguntaId === Q2_ID) setTimeout(() => simular('preguntaActual', quizPregunta3()), 2000);
    if (envio.preguntaId === Q3_ID) setTimeout(() => simular('preguntaActual', quizPregunta4()), 2000);
    if (envio.preguntaId === Q4_ID) setTimeout(() => simular('preguntaActual', quizPregunta5()), 2000);
    if (envio.preguntaId === Q5_ID) {
      setTimeout(
        () =>
          simular('salaFinalizada', {
            salaId: SALA_QUIZ_DEMO_ID,
            finalizadaUtc: new Date().toISOString(),
            motivo: 'Docente',
          }),
        2000,
      );
    }
    return resultado(envio, 'Aceptada', ahora);
  };

  return {
    ...conexion,
    invocar: async (metodo, ...argumentos) => {
      if (metodo === 'EnviarRespuesta') return enviarRespuesta(argumentos[0] as EnvioPendiente);
      if (metodo === 'RegistrarEventoFoco') {
        // El servidor real guarda el evento y avisa al docente; acá solo se deja a la vista para la demo
        console.info('[demo] RegistrarEventoFoco', argumentos[0]);
        return undefined;
      }
      if (metodo === 'ObtenerEstadoSala') {
        await esperar(200);
        if (cortada) throw new Error('Failed to fetch');
        return reportarEstado();
      }
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
      if (pin === 777777) {
        simular('estadoSala', {
          salaId: SALA_QUIZ_DEMO_ID,
          estado: 'EnCurso',
          fechaLimiteUtc: enSegundos(1800),
          preguntaActual: quizPregunta1(),
          ultimaSecuencia: 0,
          preguntasRespondidas: [],
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
