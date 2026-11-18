export type EstadoDeSala = 'Activa' | 'EnCurso' | 'Finalizada';

// Respuesta de POST /api/v1/salas (contracts/openapi/sala-en-vivo-v1.json, SalaCreada)
export type SalaCreada = {
  id: string;
  codigoPIN: number;
  fechaVencimiento: string;
  capacidadMaxima: number;
};

export type PreguntaActual = {
  salaId: string;
  preguntaId: string;
  indice: number;
  totalPreguntas: number;
  tipo: 'OpcionMultiple' | 'Abierta';
  enunciado: string;
  opciones: Array<{ id: string; texto: string }> | null;
  puntajeMaximo: number;
  fechaInicioUtc: string;
  fechaLimiteUtc: string;
};

// Evento `estadoSala` (sección 6.5). Para el docente `ultimaSecuencia` y
// `preguntasRespondidas` llegan en 0 y [], así que no se usan.
export type EstadoSalaEvento = {
  salaId: string;
  estado: EstadoDeSala;
  fechaLimiteUtc: string | null;
  preguntaActual?: PreguntaActual | null;
  servidorUtc?: string;
};

// Evento `salaIniciada` (sección 6.2)
export type SalaIniciadaEvento = {
  salaId: string;
  iniciadaUtc: string;
  totalPreguntas: number;
  fechaLimiteUtc: string;
};

// Evento `salaFinalizada` (sección 6.6). Todavía no lo emite el hub (S2-SA-03, #140).
export type SalaFinalizadaEvento = {
  salaId: string;
  finalizadaUtc: string;
  motivo: 'Docente' | 'TiempoAgotado';
};

// Evento `alumnoIngreso` (sección 6.1). Llega al entrar y también al reconectarse.
export type AlumnoIngresoEvento = {
  salaId: string;
  alumnoId: string;
  alias: string;
  totalConectados: number;
  ingresoUtc: string;
};

// Evento `alumnosConectados` (sección 6.8): foto completa de la presencia. El hub la manda cuando el docente
// se une o se reconecta, y cada vez que un alumno entra o sale.
export type AlumnosConectadosEvento = {
  salaId: string;
  totalConectados: number;
  alumnos: { alumnoId: string; alias: string; ingresoUtc: string }[];
  actualizadoUtc: string;
};

// Evento `alumnoSalio` (sección 6.9). Llega cuando se cierra la conexión activa de un alumno.
export type AlumnoSalioEvento = {
  salaId: string;
  alumnoId: string;
  alias: string;
  totalConectados: number;
  salidaUtc: string;
};

// Evento `respuestaRegistrada` (sección 6.4). `secuencia` y `estado` son opcionales:
// el contrato los define pero no se verificó que el hub los mande.
export type RespuestaRegistradaEvento = {
  salaId: string;
  alumnoId: string;
  preguntaId: string;
  secuencia?: number;
  estado?: 'Aceptada';
  recibidaUtc: string;
};

// Evento `alertaFoco`: todavía no está en el contrato, se tomó de SalaHub.cs.
// `severidad` llega como "Amarillo" o "Roja" (género distinto): se normaliza antes de usarla.
export type AlertaFocoEvento = {
  id: string;
  salaId: string;
  alumnoId: string;
  preguntaId: string | null;
  tipoEvento: string;
  duracionMs: number;
  severidad: string;
  registradoUtc: string;
};

export type SeveridadFoco = 'ninguna' | 'amarilla' | 'roja';

// Lo que muestra la lista de alumnos del panel
export type AlumnoPanel = {
  alumnoId: string;
  alias: string;
  ingresoUtc: string | null;
  alertas: number;
  peorSeveridad: SeveridadFoco;
  // Solo se completa cuando el alumno sale de la sala; al volver a entrar se quita
  desconectado?: boolean;
};

export type FaseSalaDocente = 'sinSala' | 'espera' | 'enCurso' | 'finalizada';