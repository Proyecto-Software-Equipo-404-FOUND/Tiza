import { pedir } from '@/shared/api/cliente';
import type { MiExamen, MiExamenDetalle } from '../tipos';

// Solo rol Alumno: el alumno sale del token, no se manda ni la institución ni el id.
// Docente o Administrador → 403.
export const listarMisExamenes = () => pedir<MiExamen[]>('/api/v1/alumnos/me/evaluaciones');

// S3-EV-05: detalle del examen corregido. Otra entrega → 404; nota sin aprobar → 409.
export const obtenerMiExamen = (entregaId: string) =>
  pedir<MiExamenDetalle>(`/api/v1/alumnos/me/evaluaciones/${encodeURIComponent(entregaId)}`);
