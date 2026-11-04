import { describe, expect, it } from 'vitest';
import type { Evaluacion } from './tipos';
import {
  aDatosEvaluacion,
  borradorDesde,
  borradorVacio,
  nuevaPregunta,
  sumaCriterios,
  validar,
  type EvaluacionBorrador,
} from './borrador';

const conPregunta = (cambios: Partial<ReturnType<typeof nuevaPregunta>>): EvaluacionBorrador => ({
  ...borradorVacio(),
  titulo: 'Parcial',
  preguntas: [{ ...nuevaPregunta(), enunciado: '¿Cuánto es 2 + 3?', ...cambios }],
});

describe('validar', () => {
  it('un borrador sin preguntas solo necesita título', () => {
    expect(validar({ ...borradorVacio(), titulo: 'Parcial' })).toEqual({});
    expect(validar(borradorVacio())).toEqual({ titulo: 'Ingresá un título' });
  });

  it('rechaza un título de más de 200 caracteres', () => {
    expect(validar({ ...borradorVacio(), titulo: 'x'.repeat(201) }).titulo).toBe('Máximo 200 caracteres');
  });

  it('pide enunciado, puntaje, texto de opciones y una opción correcta', () => {
    const borrador = conPregunta({ enunciado: ' ', puntajeMaximo: '-1' });
    const errores = validar(borrador);
    const [pregunta] = borrador.preguntas;
    expect(errores[`${pregunta.id}.enunciado`]).toBe('Escribí el enunciado');
    expect(errores[`${pregunta.id}.puntaje`]).toBe('Ingresá un puntaje de 0 o más');
    expect(errores[`${pregunta.id}.opciones`]).toBe('Marcá cuál es la opción correcta');
    expect(errores[`${pregunta.opciones[0].id}.texto`]).toBe('Escribí el texto de la opción');
  });

  it('pide al menos 2 opciones', () => {
    const base = conPregunta({});
    const [pregunta] = base.preguntas;
    const errores = validar({ ...base, preguntas: [{ ...pregunta, opciones: pregunta.opciones.slice(0, 1) }] });
    expect(errores[`${pregunta.id}.opciones`]).toBe('Agregá al menos 2 opciones');
  });

  it('una pregunta abierta sin rúbrica es válida; con criterios incompletos no', () => {
    expect(validar(conPregunta({ tipo: 'Abierta' }))).toEqual({});
    const criterio = { clave: 'c1', descripcion: '', puntaje: '' };
    const errores = validar(conPregunta({ tipo: 'Abierta', criterios: [criterio] }));
    expect(errores['c1.descripcion']).toBe('Describí el criterio');
    expect(errores['c1.puntaje']).toBe('Ingresá un puntaje de 0 o más');
  });
});

describe('aDatosEvaluacion', () => {
  it('arma el cuerpo del contrato para opción múltiple, sin rúbrica', () => {
    const base = conPregunta({});
    const [pregunta] = base.preguntas;
    const marcada = {
      ...base,
      preguntas: [
        {
          ...pregunta,
          puntajeMaximo: '2',
          opciones: [
            { ...pregunta.opciones[0], texto: ' 5 ', esCorrecta: true },
            { ...pregunta.opciones[1], texto: '23' },
          ],
        },
      ],
    };
    expect(aDatosEvaluacion(marcada)).toEqual({
      titulo: 'Parcial',
      tipo: 'Examen',
      preguntasJson: {
        version: 1,
        preguntas: [
          {
            id: pregunta.id,
            enunciado: '¿Cuánto es 2 + 3?',
            tipo: 'OpcionMultiple',
            puntajeMaximo: 2,
            opciones: [
              { id: pregunta.opciones[0].id, texto: '5', esCorrecta: true },
              { id: pregunta.opciones[1].id, texto: '23', esCorrecta: false },
            ],
          },
        ],
      },
    });
  });

  it('en una pregunta abierta manda la rúbrica solo si hay criterios y no manda opciones', () => {
    const sin = aDatosEvaluacion(conPregunta({ tipo: 'Abierta' })).preguntasJson.preguntas[0];
    expect(sin).not.toHaveProperty('rubrica');
    expect(sin).not.toHaveProperty('opciones');

    const con = aDatosEvaluacion(
      conPregunta({ tipo: 'Abierta', criterios: [{ clave: 'c1', descripcion: ' Define ', puntaje: '3' }] }),
    ).preguntasJson.preguntas[0];
    expect(con.rubrica).toEqual({ criterios: [{ descripcion: 'Define', puntaje: 3 }] });
  });
});

describe('borradorDesde', () => {
  it('carga una evaluación existente y se puede volver a enviar igual', () => {
    const evaluacion: Evaluacion = {
      id: 'e1',
      institucionId: 'i1',
      titulo: 'TP',
      tipo: 'Cuestionario',
      estado: 'Borrador',
      cohorteId: null,
      fechaCreacion: '2026-10-24T10:00:00Z',
      fechaActualizacion: '2026-10-24T10:00:00Z',
      version: 1,
      preguntasJson: {
        version: 1,
        preguntas: [
          {
            id: 'p1',
            enunciado: 'Explicá una variable',
            tipo: 'Abierta',
            puntajeMaximo: 10,
            rubrica: { criterios: [{ descripcion: 'Correcta', puntaje: 10 }] },
          },
        ],
      },
    };
    const borrador = borradorDesde(evaluacion);
    expect(borrador.preguntas[0].puntajeMaximo).toBe('10');
    expect(aDatosEvaluacion(borrador).preguntasJson).toEqual(evaluacion.preguntasJson);
  });
});

describe('sumaCriterios', () => {
  it('ignora los puntajes vacíos o inválidos', () => {
    expect(
      sumaCriterios([
        { clave: 'a', descripcion: '', puntaje: '3' },
        { clave: 'b', descripcion: '', puntaje: '' },
        { clave: 'c', descripcion: '', puntaje: '2.5' },
      ]),
    ).toBe(5.5);
  });
});
