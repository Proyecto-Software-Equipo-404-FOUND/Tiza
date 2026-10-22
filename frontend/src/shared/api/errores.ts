import { ApiError } from './cliente';

export function mensajeDeError(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.estado === 409)
      return 'Alguien modificó este dato. Recargá la página e intentá de nuevo.';
    if (error.estado === 403) return 'No tenés permiso para hacer esto.';
    if (error.estado === 404) return 'No se encontró lo que buscás.';
    if (error.estado === 400) return error.detalle?.detail ?? 'Revisá los datos ingresados.';
    if (error.estado === 401) return 'Tu sesión venció. Ingresá de nuevo.';
  }
  return 'Algo salió mal. Probá de nuevo.';
}
