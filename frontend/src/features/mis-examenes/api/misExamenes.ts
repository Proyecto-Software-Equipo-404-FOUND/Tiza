import { pedir } from '@/shared/api/cliente';
import type { MiExamen } from '../tipos';

// Solo rol Alumno: el alumno sale del token, no se manda ni la institución ni el id.
// Docente o Administrador → 403.
export const listarMisExamenes = () => pedir<MiExamen[]>('/api/v1/alumnos/me/evaluaciones');
