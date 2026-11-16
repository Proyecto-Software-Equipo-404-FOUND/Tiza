// Tipos según contracts/openapi/evaluaciones-v1.json (Resultados: EntregaResumen y EntregaDetalle)
// y contracts/openapi/identidad-v1.json (UsuarioResumen)

export type EstadoEntrega = 'PendienteCorreccion' | 'Corregida' | 'CorreccionManual';

export type AlertasFocoResumen = { amarillas: number; rojas: number };

export type EntregaResumen = {
  entregaId: string;
  alumnoId: string;
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
};

export type EstadoCorreccion = 'Automatica' | 'SugeridaIA' | 'PendienteIA' | 'Manual' | 'SinRespuesta';

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

export type UsuarioResumen = {
  id: string;
  nombre: string;
  apellido: string;
  email: string;
  rol: 'Administrador' | 'Docente' | 'Alumno';
};
