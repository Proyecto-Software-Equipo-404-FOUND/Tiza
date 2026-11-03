import type { Pregunta } from './tipos';

export const formatearFecha = (iso: string) => new Date(iso).toLocaleDateString('es-AR');

export const etiquetaTipoPregunta = (tipo: Pregunta['tipo']) =>
  tipo === 'OpcionMultiple' ? 'Opción múltiple' : 'Abierta';

export const puntajeTotal = (preguntas: Pregunta[]) =>
  preguntas.reduce((suma, pregunta) => suma + pregunta.puntajeMaximo, 0);

/** «1 punto» / «5 puntos» (forma corta: «1 pto» / «5 ptos») */
export function etiquetaPuntos(cantidad: number, corta = false): string {
  const unidad = corta ? (cantidad === 1 ? 'pto' : 'ptos') : cantidad === 1 ? 'punto' : 'puntos';
  return `${cantidad} ${unidad}`;
}
