import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';

// Errores de negocio de la nota final (contrato S3-EV-03): el código viene en `type` del problema.
const MENSAJES_NOTA: Record<string, string> = {
  FALTAN_CORRECCIONES: 'Todavía hay preguntas abiertas sin nota. Completalas para poder finalizar la corrección.',
  NOTA_YA_APROBADA: 'La nota ya estaba aprobada y no se puede cambiar. Actualizá la pantalla.',
  PUNTAJE_FUERA_DE_RANGO: 'Algún puntaje está fuera del rango permitido. Revisalos.',
};

export function mensajeNotaFinal(error: unknown): string {
  if (error instanceof ApiError) {
    const mensaje = error.detalle?.type ? MENSAJES_NOTA[error.detalle.type] : undefined;
    if (mensaje) return mensaje;
    if (error.estado === 403) return 'Solo el docente que creó el examen puede editar y finalizar la corrección de la nota.';
  }
  return mensajeDeError(error);
}
