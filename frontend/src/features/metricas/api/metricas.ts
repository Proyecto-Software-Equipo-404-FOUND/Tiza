import { pedir } from '@/shared/api/cliente';
import type { MetricasEvaluacion } from '../tipos';

// Solo el docente dueño del examen o un administrador; Alumno → 403. Examen ajeno o inexistente → 404.
// La institución sale del token: no se manda ni en el cuerpo ni en un header.
export const obtenerMetricas = (evaluacionId: string) =>
  pedir<MetricasEvaluacion>(`/api/v1/evaluaciones/${evaluacionId}/metricas`);
