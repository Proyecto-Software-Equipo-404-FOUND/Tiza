import { pedir } from '@/shared/api/cliente';
import type { Curso, ResultadoPaginado } from '@/shared/api/tipos';

// El back pagina de a 10 por defecto (máximo 50) y cuenta también los inactivos: sin tamanoPagina,
// un curso nuevo quedaba fuera de la primera página y no aparecía en la lista.
export const TAMANO_PAGINA_CURSOS = 50;
export const listarCursos = () =>
  pedir<ResultadoPaginado<Curso>>(`/api/v1/cursos?tamanoPagina=${TAMANO_PAGINA_CURSOS}`);

export const obtenerCurso = (id: string) => pedir<Curso>(`/api/v1/cursos/${id}`);

export const crearCurso = (datos: { nombre: string; area: string }) =>
  pedir<Curso>('/api/v1/cursos', { method: 'POST', body: JSON.stringify(datos) });

export const actualizarCurso = (id: string, datos: { nombre: string; area: string }) =>
  pedir<Curso>(`/api/v1/cursos/${id}`, { method: 'PUT', body: JSON.stringify(datos) });

// Baja lógica: el backend responde 204 sin contenido
export const eliminarCurso = (id: string) => pedir<void>(`/api/v1/cursos/${id}`, { method: 'DELETE' });
