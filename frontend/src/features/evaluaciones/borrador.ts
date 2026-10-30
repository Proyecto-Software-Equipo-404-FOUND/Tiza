import { nuevoUuid } from '@/shared/lib/uuid';
import type { Evaluacion, Pregunta, PreguntasJson } from './tipos';

// Modelo del formulario: los números viajan como texto mientras se escribe y se convierten al enviar.

export const TIPOS_EVALUACION = ['Examen', 'Cuestionario', 'Quiz'] as const;

export type OpcionBorrador = { id: string; texto: string; esCorrecta: boolean };
export type CriterioBorrador = { clave: string; descripcion: string; puntaje: string };
export type PreguntaBorrador = {
  id: string;
  enunciado: string;
  tipo: Pregunta['tipo'];
  puntajeMaximo: string;
  opciones: OpcionBorrador[];
  criterios: CriterioBorrador[];
};
export type EvaluacionBorrador = { titulo: string; tipo: string; preguntas: PreguntaBorrador[] };

/** Lo que reciben POST y PUT de /api/v1/evaluaciones (EvaluacionRequest del contrato) */
export type DatosEvaluacion = { titulo: string; tipo: string; preguntasJson: PreguntasJson };

/** Errores por campo. La clave es «titulo» o «<id de la pregunta/opción/criterio>.<campo>». */
export type ErroresBorrador = Record<string, string>;

export const nuevaOpcion = (): OpcionBorrador => ({ id: nuevoUuid(), texto: '', esCorrecta: false });
export const nuevoCriterio = (): CriterioBorrador => ({ clave: nuevoUuid(), descripcion: '', puntaje: '' });

export const nuevaPregunta = (tipo: Pregunta['tipo'] = 'OpcionMultiple'): PreguntaBorrador => ({
  id: nuevoUuid(),
  enunciado: '',
  tipo,
  puntajeMaximo: '1',
  opciones: [nuevaOpcion(), nuevaOpcion()],
  criterios: [],
});

export const borradorVacio = (): EvaluacionBorrador => ({ titulo: '', tipo: TIPOS_EVALUACION[0], preguntas: [] });

export function borradorDesde(evaluacion: Evaluacion): EvaluacionBorrador {
  return {
    titulo: evaluacion.titulo,
    tipo: evaluacion.tipo,
    preguntas: evaluacion.preguntasJson.preguntas.map((pregunta) => ({
      id: pregunta.id,
      enunciado: pregunta.enunciado,
      tipo: pregunta.tipo,
      puntajeMaximo: String(pregunta.puntajeMaximo),
      opciones:
        pregunta.opciones && pregunta.opciones.length > 0
          ? pregunta.opciones.map((opcion) => ({ ...opcion }))
          : [nuevaOpcion(), nuevaOpcion()],
      criterios: (pregunta.rubrica?.criterios ?? []).map((criterio) => ({
        clave: nuevoUuid(),
        descripcion: criterio.descripcion,
        puntaje: String(criterio.puntaje),
      })),
    })),
  };
}

const esNumeroValido = (texto: string) => texto.trim() !== '' && Number.isFinite(Number(texto)) && Number(texto) >= 0;

export const sumaCriterios = (criterios: CriterioBorrador[]) =>
  criterios.reduce((suma, criterio) => suma + (esNumeroValido(criterio.puntaje) ? Number(criterio.puntaje) : 0), 0);

export function validar(borrador: EvaluacionBorrador): ErroresBorrador {
  const errores: ErroresBorrador = {};

  const titulo = borrador.titulo.trim();
  if (!titulo) errores.titulo = 'Ingresá un título';
  else if (titulo.length > 200) errores.titulo = 'Máximo 200 caracteres';

  for (const pregunta of borrador.preguntas) {
    if (!pregunta.enunciado.trim()) errores[`${pregunta.id}.enunciado`] = 'Escribí el enunciado';
    if (!esNumeroValido(pregunta.puntajeMaximo)) errores[`${pregunta.id}.puntaje`] = 'Ingresá un puntaje de 0 o más';

    if (pregunta.tipo === 'OpcionMultiple') {
      if (pregunta.opciones.length < 2) errores[`${pregunta.id}.opciones`] = 'Agregá al menos 2 opciones';
      else if (!pregunta.opciones.some((opcion) => opcion.esCorrecta))
        errores[`${pregunta.id}.opciones`] = 'Marcá cuál es la opción correcta';
      for (const opcion of pregunta.opciones) {
        if (!opcion.texto.trim()) errores[`${opcion.id}.texto`] = 'Escribí el texto de la opción';
      }
    } else {
      for (const criterio of pregunta.criterios) {
        if (!criterio.descripcion.trim()) errores[`${criterio.clave}.descripcion`] = 'Describí el criterio';
        if (!esNumeroValido(criterio.puntaje)) errores[`${criterio.clave}.puntaje`] = 'Ingresá un puntaje de 0 o más';
      }
    }
  }
  return errores;
}

/** Arma el cuerpo del contrato. Solo se envía lo que corresponde al tipo de cada pregunta. */
export function aDatosEvaluacion(borrador: EvaluacionBorrador): DatosEvaluacion {
  const preguntas: Pregunta[] = borrador.preguntas.map((pregunta) => {
    const base = {
      id: pregunta.id,
      enunciado: pregunta.enunciado.trim(),
      tipo: pregunta.tipo,
      puntajeMaximo: Number(pregunta.puntajeMaximo),
    };
    if (pregunta.tipo === 'OpcionMultiple') {
      return {
        ...base,
        opciones: pregunta.opciones.map((opcion) => ({
          id: opcion.id,
          texto: opcion.texto.trim(),
          esCorrecta: opcion.esCorrecta,
        })),
      };
    }
    if (pregunta.criterios.length === 0) return base;
    return {
      ...base,
      rubrica: {
        criterios: pregunta.criterios.map((criterio) => ({
          descripcion: criterio.descripcion.trim(),
          puntaje: Number(criterio.puntaje),
        })),
      },
    };
  });
  return { titulo: borrador.titulo.trim(), tipo: borrador.tipo, preguntasJson: { version: 1, preguntas } };
}
