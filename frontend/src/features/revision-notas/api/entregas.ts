import { pedir } from '@/shared/api/cliente';
import type { EntregaDetalle, EntregaResumen } from '../tipos';

// Igual que el resto de Evaluaciones: se manda X-Institucion-Id hasta que la institución salga del JWT.
// Solo Docente o Administrador; Alumno → 403 (RN-CMP-01).
const conInstitucion = (institucionId: string): RequestInit => ({
  headers: { 'X-Institucion-Id': institucionId },
});

export const listarEntregas = (evaluacionId: string, institucionId: string) =>
  pedir<EntregaResumen[]>(`/api/v1/evaluaciones/${evaluacionId}/entregas`, conInstitucion(institucionId));

export const obtenerEntrega = (evaluacionId: string, entregaId: string, institucionId: string) =>
  pedir<EntregaDetalle>(`/api/v1/evaluaciones/${evaluacionId}/entregas/${entregaId}`, conInstitucion(institucionId));
