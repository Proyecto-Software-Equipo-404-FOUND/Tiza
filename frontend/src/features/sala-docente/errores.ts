import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';

// Mensajes de POST /api/v1/salas según el contrato. Lo que no se reconoce cae en el mensaje general.
export function mensajeAbrirSala(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.estado === 400) {
      // El contrato manda el texto del 400 en `title`
      return error.detalle?.title ?? error.detalle?.detail ?? 'Revisá la capacidad: tiene que estar entre 1 y 100.';
    }
    if (error.estado === 403) return 'Solo un docente o administrador puede abrir una sala.';
    if (error.estado === 404) return 'Esta evaluación no existe, no está publicada o es de otra institución.';
    if (error.estado === 503) return 'No se pudo abrir la sala. Intentá de nuevo.';
  }
  return mensajeDeError(error);
}