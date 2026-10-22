import { pedir } from '@/shared/api/cliente';
import type { Cohorte } from '@/shared/api/tipos';

export const listarCohortes = (cursoId: string) =>
  pedir<Cohorte[]>(`/api/v1/cursos/${cursoId}/cohortes`);

export const crearCohorte = (cursoId: string, datos: { nombre: string }) =>
  pedir<Cohorte>(`/api/v1/cursos/${cursoId}/cohortes`, {
    method: 'POST',
    body: JSON.stringify(datos),
  });