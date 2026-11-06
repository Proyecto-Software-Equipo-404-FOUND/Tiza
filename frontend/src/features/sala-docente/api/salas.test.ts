import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { abrirSala, validarCapacidad } from './salas';

describe('abrirSala', () => {
  const pedido = vi.fn();

  beforeEach(() => {
    pedido.mockReset();
    vi.stubGlobal('fetch', pedido);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('hace POST a /api/v1/salas con evaluacionId y capacidadMaxima, sin institución', async () => {
    const creada = {
      id: '6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9',
      codigoPIN: 482913,
      fechaVencimiento: '2026-11-13T16:30:00Z',
      capacidadMaxima: 30,
    };
    pedido.mockResolvedValue({ ok: true, status: 201, json: async () => creada });

    await expect(abrirSala({ evaluacionId: 'ev-1', capacidadMaxima: 30 })).resolves.toEqual(creada);

    const [ruta, opciones] = pedido.mock.calls[0];
    expect(ruta).toContain('/api/v1/salas');
    expect(opciones.method).toBe('POST');
    expect(JSON.parse(opciones.body)).toEqual({ evaluacionId: 'ev-1', capacidadMaxima: 30 });
    expect(opciones.headers).not.toHaveProperty('X-Institucion-Id');
  });

  it('si el servidor rechaza, lanza un ApiError con el estado', async () => {
    pedido.mockResolvedValue({
      ok: false,
      status: 404,
      json: async () => ({ title: 'No existe una evaluación publicada con ese id.', status: 404 }),
    });

    await expect(abrirSala({ evaluacionId: 'ev-x', capacidadMaxima: 40 })).rejects.toMatchObject({ estado: 404 });
  });
});

describe('validarCapacidad', () => {
  it('acepta enteros entre 1 y 100', () => {
    expect(validarCapacidad('1')).toEqual({ valor: 1 });
    expect(validarCapacidad(' 40 ')).toEqual({ valor: 40 });
    expect(validarCapacidad('100')).toEqual({ valor: 100 });
  });

  it('rechaza vacío, decimales y fuera de rango', () => {
    expect(validarCapacidad('').error).toBe('Ingresá la capacidad de la sala.');
    expect(validarCapacidad('2.5').error).toBe('Ingresá un número entero.');
    expect(validarCapacidad('0').error).toContain('entre 1 y 100');
    expect(validarCapacidad('101').error).toContain('entre 1 y 100');
  });
});