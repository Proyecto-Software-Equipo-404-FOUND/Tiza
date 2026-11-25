import { formatearNumero } from './formato';
import type { EntregaDetalle, RespuestaEntrega } from './tipos';

// Lógica de la nota final: qué preguntas edita el docente, cómo se valida un puntaje y cómo se calcula el total.

/** Solo se edita lo que corrige la IA y el alumno contestó. El múltiple choice es automático y lo no respondido vale 0. */
export const esEditable = (respuesta: RespuestaEntrega) => respuesta.tipo === 'Abierta' && Boolean(respuesta.respuesta);

/** Nota que hoy vale para una pregunta abierta: la del docente si la editó; si no, la sugerida por la IA. */
export const notaVigente = (respuesta: RespuestaEntrega) => respuesta.puntajeDocente ?? respuesta.notaSugerida ?? null;

/** Puntajes de partida (como texto, para el campo): la nota del docente, o la sugerida por la IA, o vacío. */
export function puntajesIniciales(respuestas: RespuestaEntrega[]): Record<string, string> {
  const valores: Record<string, string> = {};
  for (const respuesta of respuestas.filter(esEditable)) {
    const inicial = notaVigente(respuesta);
    valores[respuesta.preguntaId] = inicial == null ? '' : formatearNumero(inicial);
  }
  return valores;
}

export type LecturaPuntaje = { valor: number | null; error: string | null };

/** Lee lo que escribió el docente: acepta «3,5» y «3.5», de 0 hasta el máximo, con hasta 2 decimales. */
export function leerPuntaje(texto: string, maximo: number): LecturaPuntaje {
  const limpio = texto.trim().replace(',', '.');
  if (limpio === '') return { valor: null, error: 'Poné un puntaje.' };
  if (!/^\d+(\.\d{1,2})?$/.test(limpio)) return { valor: null, error: 'Escribí un número mayor o igual a 0, con hasta 2 decimales.' };
  const valor = Number(limpio);
  if (valor > maximo) return { valor: null, error: `No puede superar ${formatearNumero(maximo)}.` };
  return { valor, error: null };
}

export type ResumenNota = {
  /** Suma de todo; null si falta algún puntaje válido */
  total: number | null;
  maximo: number;
  /** Cuántas preguntas tienen el puntaje vacío o inválido */
  conProblemas: number;
};

export function resumirNota(respuestas: RespuestaEntrega[], valores: Record<string, string>): ResumenNota {
  let total = 0;
  let maximo = 0;
  let conProblemas = 0;
  for (const respuesta of respuestas) {
    maximo += respuesta.puntajeMaximo;
    if (respuesta.tipo === 'OpcionMultiple') {
      total += respuesta.puntajeObtenido ?? 0;
    } else if (esEditable(respuesta)) {
      const { valor } = leerPuntaje(valores[respuesta.preguntaId] ?? '', respuesta.puntajeMaximo);
      if (valor == null) conProblemas += 1;
      else total += valor;
    }
  }
  const redondeado = Math.round(total * 100) / 100;
  return { total: conProblemas > 0 ? null : redondeado, maximo, conProblemas };
}

export type CambioNota = { preguntaId: string; puntaje: number; comentario?: string | null };

/**
 * Los puntajes (y comentarios) que el docente cambió y hay que guardar antes de aprobar. Lo que dejó igual a la nota
 * vigente no se manda (así no queda como «Editada» una nota que solo confirmó). null si algún puntaje no es válido.
 * Si cambia el comentario se manda igual, con el puntaje vigente.
 */
export function armarCambios(
  entrega: EntregaDetalle,
  valores: Record<string, string>,
  comentarios: Record<string, string> = {},
): CambioNota[] | null {
  if (resumirNota(entrega.respuestas, valores).total == null) return null;
  const cambios: CambioNota[] = [];
  for (const respuesta of entrega.respuestas.filter(esEditable)) {
    const { valor } = leerPuntaje(valores[respuesta.preguntaId] ?? '', respuesta.puntajeMaximo);
    const anterior = (respuesta.comentarioDocente ?? '').trim();
    const escrito = comentarios[respuesta.preguntaId];
    const comentarioCambio = escrito !== undefined && escrito.trim() !== anterior;
    if (valor == null || (valor === notaVigente(respuesta) && !comentarioCambio)) continue;
    const comentario = comentarioCambio ? escrito.trim() : anterior;
    // Se manda el comentario si cambió o si ya había uno, para no borrarlo al guardar el puntaje
    if (comentarioCambio || anterior) {
      cambios.push({ preguntaId: respuesta.preguntaId, puntaje: valor, comentario: comentario === '' ? null : comentario });
    } else {
      cambios.push({ preguntaId: respuesta.preguntaId, puntaje: valor });
    }
  }
  return cambios;
}
