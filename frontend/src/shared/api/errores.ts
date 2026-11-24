import { ApiError } from './cliente';

// Errores de negocio del contrato: el servidor manda el código en `type` y el texto para el usuario lo armamos acá
const MENSAJES_POR_CODIGO: Record<string, string> = {
  // S3-CU-02: la baja de un curso con alumnos matriculados se rechaza
  CURSO_CON_ALUMNOS:
    'No se puede eliminar el curso porque tiene alumnos matriculados. Primero dá de baja sus matrículas.',
  // S3-CU-04: un alumno solo puede estar en una comisión por curso
  ALUMNO_EN_OTRA_COMISION:
    'Este alumno ya está en otra comisión de este curso. Dalo de baja de esa comisión para matricularlo acá.',
};

export function mensajeDeError(error: unknown): string {
  if (error instanceof ApiError) {
    // Si el servidor dice en qué comisión ya está el alumno, se nombra para que el administrador sepa dónde darlo de baja
    const comision = error.detalle?.cohorteActualNombre?.trim();
    if (error.detalle?.type === 'ALUMNO_EN_OTRA_COMISION' && comision) {
      return `Este alumno ya está en la comisión «${comision}» de este curso. Dalo de baja de esa comisión para matricularlo acá.`;
    }
    const porCodigo = error.detalle?.type ? MENSAJES_POR_CODIGO[error.detalle.type] : undefined;
    if (porCodigo) return porCodigo;
    if (error.estado === 409)
      return (
        error.detalle?.detail ?? 'Alguien modificó este dato. Recargá la página e intentá de nuevo.'
      );
    if (error.estado === 403) return 'No tenés permiso para hacer esto.';
    if (error.estado === 404) return 'No se encontró lo que buscás.';
    if (error.estado === 400) return error.detalle?.detail ?? 'Revisá los datos ingresados.';
    if (error.estado === 401) return 'Tu sesión venció. Ingresá de nuevo.';
    if (error.estado === 429) return 'Demasiados intentos. Esperá un momento e intentá de nuevo.';
  }
  return 'Algo salió mal. Probá de nuevo.';
}
