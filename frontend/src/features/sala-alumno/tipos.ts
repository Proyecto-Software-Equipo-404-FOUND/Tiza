// Según contracts/signalr/sala-hub-v1.md

export type CodigoSala =
  | 'NO_AUTENTICADO'
  | 'NO_AUTORIZADO'
  | 'SALA_NO_ENCONTRADA'
  | 'SALA_OTRA_INSTITUCION'
  | 'SALA_LLENA'
  | 'ALUMNO_YA_EN_SALA'
  | 'SALA_FINALIZADA'
  | 'ERROR_INTERNO';

export type EstadoDeSala = 'Activa' | 'EnCurso' | 'Finalizada';

export type OpcionPregunta = { id: string; texto: string };

// Evento `preguntaActual` (sección 6.3). No trae la respuesta correcta ni la rúbrica.
export type PreguntaActual = {
  salaId: string;
  preguntaId: string;
  indice: number;
  totalPreguntas: number;
  tipo: 'OpcionMultiple' | 'Abierta';
  enunciado: string;
  opciones: OpcionPregunta[] | null;
  puntajeMaximo: number;
  fechaInicioUtc: string;
  fechaLimiteUtc: string;
};

// Evento `estadoSala` (sección 6.5). Los campos de recuperación son opcionales por si
// el servidor todavía no los manda (S2-SA-02).
export type EstadoSalaEvento = {
  salaId: string;
  estado: EstadoDeSala;
  fechaLimiteUtc: string | null;
  preguntaActual?: PreguntaActual | null;
  ultimaSecuencia?: number;
  preguntasRespondidas?: string[];
  servidorUtc?: string;
};

// Resultado de `EnviarRespuesta` (sección 5.2.5)
export type ResultadoEnvio = {
  estado: 'Aceptada' | 'Duplicada' | 'Rechazada';
  preguntaId: string | null;
  idempotencyKey: string | null;
  secuencia: number | null;
  recibidaUtc: string;
  error: { codigo: string; mensaje: string; reintentable: boolean; correlationId?: string } | null;
};

export type EnvioPendiente = {
  preguntaId: string;
  respuesta: string;
  idempotencyKey: string;
  secuencia: number;
};

// Evento `salaIniciada` (sección 6.2)
export type SalaIniciadaEvento = {
  salaId: string;
  iniciadaUtc: string;
  totalPreguntas: number;
  fechaLimiteUtc: string;
};

// Evento `salaFinalizada` (sección 6.6)
export type SalaFinalizadaEvento = {
  salaId: string;
  finalizadaUtc: string;
  motivo: 'Docente' | 'TiempoAgotado';
};

// Evento `error` (sección 6.7)
export type ErrorAsincronoEvento = {
  codigo: string;
  mensaje: string;
  reintentable: boolean;
  correlationId?: string;
};

export type FaseSala = 'ingreso' | 'uniendo' | 'espera' | 'enCurso' | 'finalizada';
