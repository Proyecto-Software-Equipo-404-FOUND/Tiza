import { pedir } from '@/shared/api/cliente';
import type { Curso, ResultadoPaginado } from '@/shared/api/tipos';

export const listarCursos = () => pedir<ResultadoPaginado<Curso>>('/api/v1/cursos');

export const obtenerCurso = (id: string) => pedir<Curso>(`/api/v1/cursos/${id}`);

export const crearCurso = (datos: { nombre: string; area: string }) =>
  pedir<Curso>('/api/v1/cursos', { method: 'POST', body: JSON.stringify(datos) });