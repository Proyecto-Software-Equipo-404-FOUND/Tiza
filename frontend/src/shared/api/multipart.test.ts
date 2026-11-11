import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { guardarToken, pedir } from './cliente';

describe('pedir con FormData', () => {
  const pedido = vi.fn();

  beforeEach(() => {
    pedido.mockReset();
    pedido.mockResolvedValue({ ok: true, status: 201, json: async () => ({}) });
    vi.stubGlobal('fetch', pedido);
    guardarToken('token-de-prueba');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    guardarToken(null);
  });

  it('no fuerza Content-Type, para que el navegador ponga el boundary, y conserva el token', async () => {
    const datos = new FormData();
    datos.append('titulo', 'Unidad 1');
    await pedir('/api/v1/cursos/c1/materiales', { method: 'POST', body: datos });

    const [, opciones] = pedido.mock.calls[0];
    expect(opciones.body).toBe(datos);
    expect(opciones.headers).not.toHaveProperty('Content-Type');
    expect(opciones.headers.Authorization).toBe('Bearer token-de-prueba');
  });

  it('con un cuerpo JSON sigue mandando application/json', async () => {
    await pedir('/api/v1/algo', { method: 'POST', body: JSON.stringify({ a: 1 }) });
    const [, opciones] = pedido.mock.calls[0];
    expect(opciones.headers['Content-Type']).toBe('application/json');
  });
});