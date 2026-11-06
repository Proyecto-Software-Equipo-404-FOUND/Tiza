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

// El hub responde con HubException «CODIGO: mensaje». Se busca el código en cualquier parte del texto.
export function codigoDeError(falla: unknown): string {
  const texto = falla instanceof Error ? falla.message : String(falla);
  return /\b([A-Z][A-Z_]{2,}):/.exec(texto)?.[1] ?? 'ERROR_INTERNO';
}

const MENSAJES_ACCION: Record<string, string> = {
  NO_AUTORIZADO: 'No tenés permiso para conducir esta sala.',
  SALA_NO_ENCONTRADA: 'La sala ya no está disponible. Abrí una nueva.',
  SALA_YA_INICIADA: 'La sala ya estaba iniciada.',
  SIN_PREGUNTAS: 'La evaluación no tiene preguntas para tomar.',
  PREGUNTA_INEXISTENTE: 'No se encontró la pregunta siguiente.',
  SALA_NO_INICIADA: 'Primero tenés que iniciar la sala.',
  SALA_FINALIZADA: 'La sala ya finalizó.',
};

// Mensaje para Iniciar, Siguiente pregunta, Finalizar y para unirse a la sala
export function mensajeAccionSala(falla: unknown): string {
  return MENSAJES_ACCION[codigoDeError(falla)] ?? 'No pudimos completar la acción. Revisá tu conexión y probá de nuevo.';
}