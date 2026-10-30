import { pedir } from '@/shared/api/cliente';
import type { DatosEvaluacion } from '../borrador';
import type { Evaluacion } from '../tipos';

// El servicio de Evaluaciones pide X-Institucion-Id hasta que la institución se lea del JWT (ver contrato)
const conInstitucion = (institucionId: string): RequestInit => ({
  headers: { 'X-Institucion-Id': institucionId },
});

export const listarEvaluaciones = (institucionId: string) =>
  pedir<Evaluacion[]>('/api/v1/evaluaciones', conInstitucion(institucionId));

export const obtenerEvaluacion = (id: string, institucionId: string) =>
  pedir<Evaluacion>(`/api/v1/evaluaciones/${id}`, conInstitucion(institucionId));

// Crea la evaluación en estado Borrador (201)
export const crearEvaluacion = (institucionId: string, datos: DatosEvaluacion) =>
  pedir<Evaluacion>('/api/v1/evaluaciones', {
    ...conInstitucion(institucionId),
    method: 'POST',
    body: JSON.stringify(datos),
  });

// Solo se puede modificar una evaluación en Borrador: si no, el servicio responde 409
export const actualizarEvaluacion = (id: string, institucionId: string, datos: DatosEvaluacion) =>
  pedir<Evaluacion>(`/api/v1/evaluaciones/${id}`, {
    ...conInstitucion(institucionId),
    method: 'PUT',
    body: JSON.stringify(datos),
  });
