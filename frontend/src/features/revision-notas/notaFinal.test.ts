import { describe, expect, it } from 'vitest';
import { armarCambios, esEditable, leerPuntaje, puntajesIniciales, resumirNota } from './notaFinal';
import type { EntregaDetalle, RespuestaEntrega } from './tipos';

const multiple: RespuestaEntrega = {
  preguntaId: 'p1',
  enunciado: 'MC',
  tipo: 'OpcionMultiple',
  puntajeMaximo: 2,
  respuesta: 'o1',
  puntajeObtenido: 2,
};
const abierta = (cambios: Partial<RespuestaEntrega> = {}): RespuestaEntrega => ({
  preguntaId: 'p2',
  enunciado: 'Abierta',
  tipo: 'Abierta',
  puntajeMaximo: 4,
  respuesta: 'Texto',
  notaSugerida: 3.5,
  ...cambios,
});

describe('esEditable', () => {
  it('solo las abiertas que el alumno contestó', () => {
    expect(esEditable(multiple)).toBe(false);
    expect(esEditable(abierta())).toBe(true);
    expect(esEditable(abierta({ respuesta: null }))).toBe(false);
  });
});

describe('puntajesIniciales', () => {
  it('usa la nota del docente, si no la sugerida, si no vacío', () => {
    expect(puntajesIniciales([abierta()])).toEqual({ p2: '3,5' });
    expect(puntajesIniciales([abierta({ puntajeDocente: 2 })])).toEqual({ p2: '2' });
    expect(puntajesIniciales([abierta({ notaSugerida: null })])).toEqual({ p2: '' });
  });

  it('no incluye el múltiple choice ni lo no respondido', () => {
    expect(puntajesIniciales([multiple, abierta({ respuesta: null })])).toEqual({});
  });
});

describe('leerPuntaje', () => {
  it('acepta coma y punto', () => {
    expect(leerPuntaje('3,5', 4)).toEqual({ valor: 3.5, error: null });
    expect(leerPuntaje(' 3.5 ', 4)).toEqual({ valor: 3.5, error: null });
    expect(leerPuntaje('0', 4)).toEqual({ valor: 0, error: null });
  });

  it('rechaza vacío, negativos, texto y más de 2 decimales', () => {
    expect(leerPuntaje('', 4).valor).toBeNull();
    expect(leerPuntaje('-1', 4).valor).toBeNull();
    expect(leerPuntaje('abc', 4).valor).toBeNull();
    expect(leerPuntaje('1,234', 4).valor).toBeNull();
  });

  it('rechaza lo que supera el máximo y lo dice', () => {
    expect(leerPuntaje('4,5', 4)).toEqual({ valor: null, error: 'No puede superar 4.' });
    expect(leerPuntaje('4', 4).valor).toBe(4);
  });
});

describe('resumirNota', () => {
  it('suma el múltiple choice, las abiertas editadas y lo no respondido como 0', () => {
    const respuestas = [multiple, abierta(), abierta({ preguntaId: 'p3', respuesta: null, puntajeMaximo: 2 })];
    expect(resumirNota(respuestas, { p2: '3,5' })).toEqual({ total: 5.5, maximo: 8, conProblemas: 0 });
  });

  it('sin total si falta algún puntaje válido', () => {
    expect(resumirNota([multiple, abierta()], { p2: '' })).toEqual({ total: null, maximo: 6, conProblemas: 1 });
    expect(resumirNota([multiple, abierta()], { p2: '9' }).total).toBeNull();
  });

  it('evita errores de redondeo al sumar decimales', () => {
    const respuestas = [abierta({ preguntaId: 'a' }), abierta({ preguntaId: 'b' })];
    expect(resumirNota(respuestas, { a: '0,1', b: '0,2' }).total).toBe(0.3);
  });
});

describe('armarCambios', () => {
  const entrega = { respuestas: [multiple, abierta()] } as EntregaDetalle;

  it('si el docente dejó lo que sugirió la IA no hay nada que guardar', () => {
    expect(armarCambios(entrega, { p2: '3,5' })).toEqual([]);
  });

  it('manda solo lo que cambió respecto de la nota vigente', () => {
    expect(armarCambios(entrega, { p2: '4' })).toEqual([{ preguntaId: 'p2', puntaje: 4 }]);
    const editada = { respuestas: [multiple, abierta({ puntajeDocente: 2 })] } as EntregaDetalle;
    expect(armarCambios(editada, { p2: '2' })).toEqual([]);
    expect(armarCambios(editada, { p2: '3,5' })).toEqual([{ preguntaId: 'p2', puntaje: 3.5 }]);
  });

  it('una pregunta que la IA no corrigió siempre se guarda', () => {
    const pendiente = { respuestas: [multiple, abierta({ notaSugerida: null })] } as EntregaDetalle;
    expect(armarCambios(pendiente, { p2: '2,5' })).toEqual([{ preguntaId: 'p2', puntaje: 2.5 }]);
  });

  it('devuelve null si algo no es válido', () => {
    expect(armarCambios(entrega, { p2: '' })).toBeNull();
    expect(armarCambios(entrega, { p2: '9' })).toBeNull();
  });
});
