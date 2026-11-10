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