import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';

const ESTADOS_CON_TEXTO_DEL_SERVIDOR = [400, 403, 404, 415];

const TEXTO_POR_DEFECTO: Record<number, string> = {
  400: 'Revisá el archivo e intentá de nuevo.',
  403: 'No tenés permiso para gestionar el material de este curso.',
  404: 'No se encontró lo que buscás.',
  415: 'Solo se aceptan archivos PDF.',
};

export function mensajeApuntes(error: unknown): string {
  if (error instanceof ApiError && ESTADOS_CON_TEXTO_DEL_SERVIDOR.includes(error.estado)) {
    return error.detalle?.title ?? TEXTO_POR_DEFECTO[error.estado];
  }
  return mensajeDeError(error);
}

// Vista previa del texto. El 409 se resuelve por el status: el backend todavía puede mandarlo sin `type` (#262)
export function mensajeTextoApunte(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.estado === 409) {
      return 'El apunte todavía se está procesando o falló la extracción. Probá de nuevo en unos minutos.';
    }
    if (error.estado === 403) {
      return error.detalle?.title ?? 'Solo el docente del curso o un administrador pueden ver el texto.';
    }
    if (error.estado === 404) {
      return error.detalle?.title ?? 'No se encontró el apunte.';
    }
  }
  return mensajeDeError(error);
}