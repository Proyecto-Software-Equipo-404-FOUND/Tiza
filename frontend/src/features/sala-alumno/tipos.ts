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

// Evento `estadoSala` (sección 6.5). Se tipan solo los campos que usa esta pantalla.
export type EstadoSalaEvento = {
  salaId: string;
  estado: EstadoDeSala;
  fechaLimiteUtc: string | null;
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
