// Forma de GET /api/v1/alumnos/me/evaluaciones (MiExamen en contracts/openapi/evaluaciones-v1.json)
export type EstadoMiExamen = 'PendienteCorreccion' | 'Aprobada';

export type MiExamen = {
  evaluacionId: string;
  entregaId: string;
  cohorteId?: string | null;
  titulo: string;
  fechaEntregaUtc: string;
  estado: EstadoMiExamen;
  /** Solo viene si estado = Aprobada */
  notaFinal?: number | null;
  /** Solo viene si estado = Aprobada */
  puntajeMaximo?: number | null;
};

// Forma de GET /api/v1/alumnos/me/evaluaciones/{entregaId} (MiExamenDetalle en evaluaciones-v1.json, S3-EV-05).
// Solo llega con la nota aprobada. Nunca trae el comentario interno del docente.
export type MiRespuesta = {
  preguntaId: string;
  enunciado: string;
  tipo: 'OpcionMultiple' | 'Abierta';
  puntajeMaximo: number;
  /** Abierta: lo que escribió el alumno */
  respuesta?: string | null;
  /** Opción múltiple: la que eligió (null si no respondió) */
  opcionElegidaTexto?: string | null;
  opcionCorrectaTexto?: string | null;
  /** Opción múltiple: si eligió la correcta. Abierta: null */
  esCorrecta?: boolean | null;
  puntajeObtenido?: number | null;
  /** Abierta: la justificación de la IA (null si el docente editó el puntaje) */
  justificacion?: string | null;
};

export type MiExamenDetalle = {
  evaluacionId: string;
  entregaId: string;
  titulo: string;
  fechaEntregaUtc: string;
  notaFinal?: number | null;
  puntajeMaximo?: number | null;
  preguntas: MiRespuesta[];
};
