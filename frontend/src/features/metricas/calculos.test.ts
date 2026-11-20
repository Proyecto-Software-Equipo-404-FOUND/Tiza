import { describe, expect, it } from 'vitest';
import { describirDiferencia, porcentajeParaBarra, preguntaMasDificil } from './calculos';
import type { MetricasEvaluacion } from './tipos';

describe('describirDiferencia', () => {
  it('por encima del promedio', () => {
    expect(describirDiferencia(2.5)).toEqual({ texto: '2,5 puntos por encima del promedio', tono: 'arriba' });
  });

  it('por debajo del promedio, sin el signo menos', () => {
    expect(describirDiferencia(-0.83)).toEqual({ texto: '0,83 puntos por debajo del promedio', tono: 'abajo' });
  });

  it('usa «punto» en singular cuando la diferencia es exactamente 1', () => {
    expect(describirDiferencia(1)).toEqual({ texto: '1 punto por encima del promedio', tono: 'arriba' });
    expect(describirDiferencia(-1)).toEqual({ texto: '1 punto por debajo del promedio', tono: 'abajo' });
  });

  it('una diferencia que se redondea a cero cuenta como «en el promedio»', () => {
    expect(describirDiferencia(0)).toEqual({ texto: 'En el promedio', tono: 'igual' });
    expect(describirDiferencia(0.004)).toEqual({ texto: 'En el promedio', tono: 'igual' });
  });
});

describe('porcentajeParaBarra', () => {
  it('redondea y acota entre 0 y 100', () => {
    expect(porcentajeParaBarra(66.67)).toBe(67);
    expect(porcentajeParaBarra(-5)).toBe(0);
    expect(porcentajeParaBarra(130)).toBe(100);
  });
});

describe('preguntaMasDificil', () => {
  const base: MetricasEvaluacion = {
    evaluacionId: 'e1',
    cantidadEntregas: 1,
    cantidadAprobadas: 1,
    puntajeMaximo: 4,
    promedio: 2,
    preguntaMasDificilId: 'p2',
    preguntas: [
      { preguntaId: 'p1', enunciado: 'Uno', puntajeMaximo: 2, promedio: 2, porcentaje: 100 },
      { preguntaId: 'p2', enunciado: 'Dos', puntajeMaximo: 2, promedio: 0, porcentaje: 0 },
    ],
    alumnos: [],
  };

  it('encuentra la pregunta que marca el servidor', () => {
    expect(preguntaMasDificil(base)?.enunciado).toBe('Dos');
  });

  it('sin id o con un id desconocido devuelve undefined', () => {
    expect(preguntaMasDificil({ ...base, preguntaMasDificilId: null })).toBeUndefined();
    expect(preguntaMasDificil({ ...base, preguntaMasDificilId: 'x' })).toBeUndefined();
  });
});
