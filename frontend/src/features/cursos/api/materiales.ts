import { pedir } from '@/shared/api/cliente';
import type { Material } from '../apuntes';

export const listarMateriales = (cursoId: string) =>
  pedir<Material[]>(`/api/v1/cursos/${cursoId}/materiales`);

// multipart/form-data: `archivo` y `titulo` (opcional; si falta, el servidor usa el nombre del archivo)
export const subirMaterial = (cursoId: string, archivo: File, titulo?: string) => {
  const datos = new FormData();
  datos.append('archivo', archivo);
  if (titulo?.trim()) datos.append('titulo', titulo.trim());
  return pedir<Material>(`/api/v1/cursos/${cursoId}/materiales`, { method: 'POST', body: datos });
};

// Baja lógica: el backend responde 204 sin contenido
export const eliminarMaterial = (cursoId: string, id: string) =>
  pedir<void>(`/api/v1/cursos/${cursoId}/materiales/${id}`, { method: 'DELETE' });