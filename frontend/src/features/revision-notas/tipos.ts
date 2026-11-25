// Tipos según contracts/openapi/evaluaciones-v1.json (Resultados: EntregaResumen y EntregaDetalle)
// y contracts/openapi/identidad-v1.json (UsuarioResumen)

export type EstadoEntrega = 'PendienteCorreccion' | 'Corregida' | 'CorreccionManual';

export type AlertasFocoResumen = { amarillas: number; rojas: number };

export type EntregaResumen = {
  entregaId: string;
  alumnoId: string;
  /** Solo para invitados (alumnos sin cuenta): el nombre que escribieron al entrar */
  nombreInvitado?: string | null;
  salaId: string;
  estado: EstadoEntrega;
  respondioAlgo?: boolean;
  notaMultipleChoice?: number | null;
  puntajeMaximoMultipleChoice?: number | null;
  /** Suma de las notas SUGERIDAS por la IA. Nunca es la nota final (RN-CMP-03). */
  notaSugeridaAbiertas?: number | null;
  puntajeMaximoAbiertas?: number | null;
  fechaEntregaUtc: string;
  alertasFoco: AlertasFocoResumen;
  /** Sugerida: solo hay nota automática o de la IA. Editada: el docente cambió algún puntaje. Aprobada: nota cerrada. */
  estadoNota?: EstadoNota;
  /** Solo cuando estadoNota = Aprobada */
  notaFinal?: number | null;
  /** Cuándo se aprobó; solo cuando estadoNota = Aprobada */
  fechaAprobacionUtc?: string | null;
};

export type EstadoCorreccion = 'Automatica' | 'SugeridaIA' | 'PendienteIA' | 'Manual' | 'SinRespuesta' | 'Editada';

export type EstadoNota = 'Sugerida' | 'Editada' | 'Aprobada';

export type DesgloseCriterio = { criterio?: string; puntaje?: number; comentario?: string };

export type RespuestaEntrega = {
  preguntaId: string;
  enunciado: string;
  tipo: 'OpcionMultiple' | 'Abierta';
  puntajeMaximo: number;
  respuesta?: string | null;
  opcionElegidaTexto?: string | null;
  esCorrecta?: boolean | null;
  puntajeObtenido?: number | null;
  notaSugerida?: number | null;
  justificacionIA?: string | null;
  desglosePorCriterio?: DesgloseCriterio[] | null;
  /** Puntaje que puso el docente al editar; si existe, manda sobre la nota automática y la de la IA (S3-EV-03). */
  puntajeDocente?: number | null;
  comentarioDocente?: string | null;
  estadoCorreccion?: EstadoCorreccion;
};

export type AlertaFocoDetalle = {
  preguntaId?: string | null;
  tipoEvento: string;
  duracionMs: number;
  // El contrato escribe «Amarillo» y «Roja»: se compara sin depender de la forma exacta
  severidad: string;
  registradoUtc: string;
};

export type EntregaDetalle = EntregaResumen & {
  respuestas: RespuestaEntrega[];
  alertas: AlertaFocoDetalle[];
};

// Contrato S3-EV-03 (PR #253): el docente edita una pregunta a la vez y después aprueba sin cuerpo.
export type EditarNotaRequest = { puntaje: number; comentario?: string | null };
export type NotaAprobada = {
  entregaId: string;
  notaFinal: number;
  puntajeMaximo: number;
  aprobadaPorId: string;
  fechaAprobacionUtc: string;
};

export type UsuarioResumen = {
  id: string;
  nombre: string;
  apellido: string;
  email: string;
  rol: 'Administrador' | 'Docente' | 'Alumno';
};
