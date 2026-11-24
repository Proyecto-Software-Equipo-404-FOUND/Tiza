import { describe, expect, it } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { borradorDesdeSugerida, mensajeDeSugerencia, type PreguntaSugerida } from './sugerencias';

const opcionMultiple: PreguntaSugerida = {
  enunciado: '¿Qué hace un condicional?',
  tipo: 'OpcionMultiple',
  puntajeMaximo: 1,
  opciones: [
    { texto: 'Decide qué se ejecuta', esCorrecta: true },
    { texto: 'Repite sin límite', esCorrecta: false },
    { texto: 'Declara una variable', esCorrecta: false },
  ],
  fragmentoApunte: 'Las estructuras de control permiten decidir.',
};

const abierta: PreguntaSugerida = {
  enunciado: 'Explicá para qué sirve un bucle.',
  tipo: 'Abierta',
  puntajeMaximo: 2,
  rubrica: {
    criterios: [
      { descripcion: 'Explica que repite', puntaje: 1 },
      { descripcion: 'Da un ejemplo', puntaje: 1 },
    ],
  },
};

describe('borradorDesdeSugerida', () => {
  it('convierte una de opción múltiple: puntaje como texto, ids nuevos y una sola correcta', () => {
    const pregunta = borradorDesdeSugerida(opcionMultiple);

    expect(pregunta.enunciado).toBe('¿Qué hace un condicional?');
    expect(pregunta.tipo).toBe('OpcionMultiple');
    expect(pregunta.puntajeMaximo).toBe('1');
    expect(pregunta.opciones.map((opcion) => [opcion.texto, opcion.esCorrecta])).toEqual([
      ['Decide qué se ejecuta', true],
      ['Repite sin límite', false],
      ['Declara una variable', false],
    ]);
    expect(pregunta.criterios).toEqual([]);
    expect(new Set(pregunta.opciones.map((opcion) => opcion.id)).size).toBe(3);
  });

  it('si la IA marca varias correctas, queda solo la primera', () => {
    const pregunta = borradorDesdeSugerida({
      ...opcionMultiple,
      opciones: [
        { texto: 'A', esCorrecta: false },
        { texto: 'B', esCorrecta: true },
        { texto: 'C', esCorrecta: true },
      ],
    });
    expect(pregunta.opciones.map((opcion) => opcion.esCorrecta)).toEqual([false, true, false]);
  });

  it('si la IA no marca ninguna correcta, no inventa una (la validación lo avisa al guardar)', () => {
    const pregunta = borradorDesdeSugerida({
      ...opcionMultiple,
      opciones: [
        { texto: 'A', esCorrecta: false },
        { texto: 'B', esCorrecta: false },
      ],
    });
    expect(pregunta.opciones.some((opcion) => opcion.esCorrecta)).toBe(false);
  });

  it('una de opción múltiple sin opciones arranca con 2 vacías', () => {
    const pregunta = borradorDesdeSugerida({ enunciado: 'X', tipo: 'OpcionMultiple', puntajeMaximo: 1 });
    expect(pregunta.opciones).toHaveLength(2);
    expect(pregunta.opciones.every((opcion) => opcion.texto === '' && !opcion.esCorrecta)).toBe(true);
  });

  it('convierte una abierta con rúbrica: criterios con puntaje como texto', () => {
    const pregunta = borradorDesdeSugerida(abierta);

    expect(pregunta.tipo).toBe('Abierta');
    expect(pregunta.puntajeMaximo).toBe('2');
    expect(pregunta.criterios.map((criterio) => [criterio.descripcion, criterio.puntaje])).toEqual([
      ['Explica que repite', '1'],
      ['Da un ejemplo', '1'],
    ]);
    expect(new Set(pregunta.criterios.map((criterio) => criterio.clave)).size).toBe(2);
  });

  it('una abierta sin rúbrica queda sin criterios', () => {
    const pregunta = borradorDesdeSugerida({ enunciado: 'X', tipo: 'Abierta', puntajeMaximo: 3 });
    expect(pregunta.criterios).toEqual([]);
  });

  it('una abierta ignora las opciones si la IA las manda', () => {
    const pregunta = borradorDesdeSugerida({ ...abierta, opciones: [{ texto: 'A', esCorrecta: true }] });
    expect(pregunta.opciones.some((opcion) => opcion.esCorrecta)).toBe(false);
  });

  it('cada conversión genera ids nuevos', () => {
    expect(borradorDesdeSugerida(opcionMultiple).id).not.toBe(borradorDesdeSugerida(opcionMultiple).id);
  });
});

describe('mensajeDeSugerencia', () => {
  it('avisa cuando se corta por tiempo', () => {
    expect(mensajeDeSugerencia(new DOMException('cortado', 'AbortError'))).toMatch(/tardó más de lo esperado/);
    expect(mensajeDeSugerencia(new DOMException('cortado', 'TimeoutError'))).toMatch(/tardó más de lo esperado/);
  });

  it('traduce los códigos del contrato por su `type`', () => {
    expect(
      mensajeDeSugerencia(new ApiError(422, { status: 422, type: 'MATERIAL_SIN_TEXTO', title: 'sin texto' })),
    ).toMatch(/no tiene texto para generar preguntas/);
    expect(
      mensajeDeSugerencia(new ApiError(404, { status: 404, type: 'MATERIAL_NO_ENCONTRADO', title: 'x' })),
    ).toMatch(/No encontramos el apunte/);
    expect(
      mensajeDeSugerencia(new ApiError(503, { status: 503, type: 'IA_NO_DISPONIBLE', title: 'x' })),
    ).toBe('La IA no está disponible. Probá de nuevo en unos minutos.');
  });

  it('en un 400 muestra el `title` del servidor', () => {
    expect(
      mensajeDeSugerencia(new ApiError(400, { status: 400, title: 'La cantidad tiene que estar entre 1 y 10.' })),
    ).toBe('La cantidad tiene que estar entre 1 y 10.');
  });

  it('en un 403 dice que es solo para docentes', () => {
    expect(mensajeDeSugerencia(new ApiError(403, { status: 403, title: 'x' }))).toBe(
      'Solo los docentes pueden pedir sugerencias.',
    );
  });

  it('ante una caída de red avisa de la conexión', () => {
    expect(mensajeDeSugerencia(new TypeError('Failed to fetch'))).toMatch(/No pudimos conectarnos/);
  });

  it('para el resto usa el mensaje genérico', () => {
    expect(mensajeDeSugerencia(new ApiError(500, { status: 500 }))).toBe('Algo salió mal. Probá de nuevo.');
    expect(mensajeDeSugerencia(new Error('x'))).toBe('Algo salió mal. Probá de nuevo.');
  });
});
