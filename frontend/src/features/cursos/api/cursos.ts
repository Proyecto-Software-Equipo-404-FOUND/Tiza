import { pedir } from '@/shared/api/cliente';
import type { Curso } from '@/shared/api/tipos';

export const listarCursos = () => pedir<Curso[]>('/api/v1/cursos');

export const crearCurso = (datos: { nombre: string }) =>
  pedir<Curso>('/api/v1/cursos', { method: 'POST', body: JSON.stringify(datos) });