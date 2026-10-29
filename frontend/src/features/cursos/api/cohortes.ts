import { pedir } from '@/shared/api/cliente';
import type { Cohorte } from '@/shared/api/tipos';

export const listarCohortes = (cursoId: string) =>
  pedir<Cohorte[]>(`/api/v1/cursos/${cursoId}/cohortes`);

export const crearCohorte = (
  cursoId: string,
  datos: { nombre: string; periodo: string; docenteId: string },
) =>
  pedir<Cohorte>(`/api/v1/cursos/${cursoId}/cohortes`, {
    method: 'POST',
    body: JSON.stringify(datos),
  });

export const actualizarCohorte = (
  cursoId: string,
  id: string,
  datos: { nombre: string; periodo: string; docenteId: string },
) =>
  pedir<Cohorte>(`/api/v1/cursos/${cursoId}/cohortes/${id}`, {
    method: 'PUT',
    body: JSON.stringify(datos),
  });

// Baja lógica (cierre de comisión): el backend responde 204 sin contenido
export const eliminarCohorte = (cursoId: string, id: string) =>
  pedir<void>(`/api/v1/cursos/${cursoId}/cohortes/${id}`, { method: 'DELETE' });
