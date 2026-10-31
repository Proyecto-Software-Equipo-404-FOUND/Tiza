import { pedir } from '@/shared/api/cliente';
import type { Evaluacion } from '../tipos';

// El servicio de Evaluaciones pide X-Institucion-Id hasta que la institución se lea del JWT (ver contrato)
const conInstitucion = (institucionId: string): RequestInit => ({
  headers: { 'X-Institucion-Id': institucionId },
});

export const listarEvaluaciones = (institucionId: string) =>
  pedir<Evaluacion[]>('/api/v1/evaluaciones', conInstitucion(institucionId));

export const obtenerEvaluacion = (id: string, institucionId: string) =>
  pedir<Evaluacion>(`/api/v1/evaluaciones/${id}`, conInstitucion(institucionId));
