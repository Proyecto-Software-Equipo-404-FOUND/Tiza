import { describe, expect, it } from 'vitest';
import { notaSobreDiez, textoNotaAlumno } from './notaSobreDiez';

describe('notaSobreDiez', () => {
  it('lleva la nota a escala 10', () => {
    expect(notaSobreDiez(4, 5)).toBe(8);
    expect(notaSobreDiez(8.5, 10)).toBe(8.5);
    expect(notaSobreDiez(0, 5)).toBe(0);
  });

  it('redondea a un decimal', () => {
    expect(notaSobreDiez(7, 9)).toBe(7.8);
  });

  it('devuelve null si falta la nota o el máximo no es válido', () => {
    expect(notaSobreDiez(null, 5)).toBeNull();
    expect(notaSobreDiez(4, null)).toBeNull();
    expect(notaSobreDiez(4, 0)).toBeNull();
  });
});

describe('textoNotaAlumno', () => {
  it('si el examen ya es sobre 10 no agrega detalle', () => {
    expect(textoNotaAlumno(8.5, 10)).toEqual({ principal: '8,5 / 10', detalle: null });
  });

  it('si el examen es sobre otro máximo muestra la nota sobre 10 y el puntaje original', () => {
    expect(textoNotaAlumno(4, 5)).toEqual({ principal: '8 / 10', detalle: '4 / 5 puntos' });
  });

  it('sin nota no muestra nada', () => {
    expect(textoNotaAlumno(null, 5)).toBeNull();
  });
});
