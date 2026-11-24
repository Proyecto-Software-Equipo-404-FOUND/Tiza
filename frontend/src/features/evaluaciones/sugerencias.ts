import { nuevaOpcion, type PreguntaBorrador } from './borrador';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { nuevoUuid } from '@/shared/lib/uuid';
import type { Pregunta, Rubrica } from './tipos';

// Tipos según contracts/openapi/ia-v1.json (SugerirPreguntasRequest, PreguntaSugerida, SugerirPreguntasResponse)
export type TipoPregunta = Pregunta['tipo'];

export type DatosSugerencia = {
  materialId: string;
  cantidad?: number;
  tipos?: TipoPregunta[];
  tema?: string;
};

export type PreguntaSugerida = {
  enunciado: string;
  tipo: TipoPregunta;
  puntajeMaximo: number;
  opciones?: { texto: string; esCorrecta: boolean }[];
  rubrica?: Rubrica;
  /** Parte del apunte en la que se basa: se muestra para que el docente la verifique, no se guarda */
  fragmentoApunte?: string | null;
};

export type RespuestaSugerencias = {
  materialId: string;
  preguntas: PreguntaSugerida[];
  proveedor: string;
  modelo: string;
  revisadaPorAgenteRevisor?: boolean;
};

// Lo mínimo que se necesita de un apunte (Material vive en features/cursos y no se puede importar)
export type ApunteSugerible = { id: string; titulo: string; estado: 'Procesando' | 'Listo' | 'Error' };

export const CANTIDAD_MINIMA = 1;
export const CANTIDAD_MAXIMA = 10;
export const CANTIDAD_POR_DEFECTO = 5;
export const TEMA_MAXIMO = 200;

/** Convierte una sugerencia en una pregunta del formulario (ids nuevos, números como texto). */
export function borradorDesdeSugerida(sugerida: PreguntaSugerida): PreguntaBorrador {
  const opcionesSugeridas = sugerida.tipo === 'OpcionMultiple' ? (sugerida.opciones ?? []) : [];
  const indiceCorrecta = opcionesSugeridas.findIndex((opcion) => opcion.esCorrecta);

  return {
    id: nuevoUuid(),
    enunciado: sugerida.enunciado,
    tipo: sugerida.tipo,
    puntajeMaximo: String(sugerida.puntajeMaximo),
    opciones:
      opcionesSugeridas.length > 0
        ? opcionesSugeridas.map((opcion, indice) => ({
            id: nuevoUuid(),
            texto: opcion.texto,
            // El editor permite una sola correcta: si la IA marcó varias, queda la primera
            esCorrecta: indice === indiceCorrecta,
          }))
        : [nuevaOpcion(), nuevaOpcion()],
    criterios: (sugerida.tipo === 'Abierta' ? (sugerida.rubrica?.criterios ?? []) : []).map((criterio) => ({
      clave: nuevoUuid(),
      descripcion: criterio.descripcion,
      puntaje: String(criterio.puntaje),
    })),
  };
}

/** Mensaje para el docente según el código del contrato (`type`) o el estado HTTP. */
export function mensajeDeSugerencia(error: unknown): string {
  if (error instanceof DOMException && (error.name === 'AbortError' || error.name === 'TimeoutError')) {
    return 'La IA tardó más de lo esperado. Probá de nuevo en unos minutos.';
  }
  if (error instanceof ApiError) {
    switch (error.detalle?.type) {
      case 'MATERIAL_SIN_TEXTO':
        return 'Este apunte no tiene texto para generar preguntas (puede ser un PDF escaneado). Elegí otro apunte.';
      case 'MATERIAL_NO_ENCONTRADO':
        return 'No encontramos el apunte. Puede que lo hayan eliminado: elegí otro.';
      case 'IA_NO_DISPONIBLE':
        return 'La IA no está disponible. Probá de nuevo en unos minutos.';
    }
    if (error.estado === 400) return error.detalle?.title ?? 'Revisá los datos ingresados.';
    if (error.estado === 403) return 'Solo los docentes pueden pedir sugerencias.';
  }
  if (error instanceof TypeError) return 'No pudimos conectarnos. Revisá tu conexión y probá de nuevo.';
  return mensajeDeError(error);
}
