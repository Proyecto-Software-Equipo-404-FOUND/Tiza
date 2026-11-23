import { pedir } from '@/shared/api/cliente';
import type { EditarNotaRequest, NotaAprobada, RespuestaEntrega } from '../tipos';

// Contrato S3-EV-03 (PR #253, planificado). Solo el docente dueño del examen; el administrador recibe 403.
const conInstitucion = (institucionId: string): RequestInit => ({
  headers: { 'X-Institucion-Id': institucionId },
});

export const editarNotaRespuesta = (
  evaluacionId: string,
  entregaId: string,
  preguntaId: string,
  institucionId: string,
  pedido: EditarNotaRequest,
) =>
  pedir<RespuestaEntrega>(
    `/api/v1/evaluaciones/${evaluacionId}/entregas/${entregaId}/respuestas/${preguntaId}/nota`,
    { ...conInstitucion(institucionId), method: 'PUT', body: JSON.stringify(pedido) },
  );

// Sin cuerpo: el backend calcula la nota final (puntaje del docente, o el automático, o el sugerido por la IA)
export const aprobarNota = (evaluacionId: string, entregaId: string, institucionId: string) =>
  pedir<NotaAprobada>(`/api/v1/evaluaciones/${evaluacionId}/entregas/${entregaId}/aprobacion`, {
    ...conInstitucion(institucionId),
    method: 'POST',
  });
