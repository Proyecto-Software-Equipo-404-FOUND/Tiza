import type { Pregunta } from './tipos';

export const formatearFecha = (iso: string) => new Date(iso).toLocaleDateString('es-AR');

export const etiquetaTipoPregunta = (tipo: Pregunta['tipo']) =>
  tipo === 'OpcionMultiple' ? 'Opción múltiple' : 'Abierta';

export const puntajeTotal = (preguntas: Pregunta[]) =>
  preguntas.reduce((suma, pregunta) => suma + pregunta.puntajeMaximo, 0);
