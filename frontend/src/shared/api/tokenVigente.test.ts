import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { guardarToken, guardarTokenRenovacion, obtenerTokenVigente } from './cliente';

// JWT de mentira: solo importa el claim `exp` (segundos desde 1970)
const jwtQueVenceEn = (segundos: number) =>
  `cabecera.${btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + segundos }))}.firma`;

const respuestaDeRenovacion = (accessToken: string) => ({
  ok: true,
  json: async () => ({ accessToken, refreshToken: 'refresh-nuevo' }),
});

describe('obtenerTokenVigente', () => {
  const pedido = vi.fn();

  beforeEach(() => {
    pedido.mockReset();
    vi.stubGlobal('fetch', pedido);
    guardarTokenRenovacion('refresh-viejo');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    guardarToken(null);
  });

  it('con el token vigente lo devuelve tal cual y no renueva', async () => {
    const vigente = jwtQueVenceEn(600);
    guardarToken(vigente);

    await expect(obtenerTokenVigente()).resolves.toBe(vigente);
    expect(pedido).not.toHaveBeenCalled();
  });

  it('con el token vencido pide uno nuevo con el refresh token y devuelve el nuevo', async () => {
    guardarToken(jwtQueVenceEn(-10));
    pedido.mockResolvedValue(respuestaDeRenovacion('token-nuevo'));

    await expect(obtenerTokenVigente()).resolves.toBe('token-nuevo');
    expect(pedido).toHaveBeenCalledTimes(1);
    expect(pedido.mock.calls[0][0]).toContain('/api/v1/identidad/auth/refresh');
    expect(JSON.parse(pedido.mock.calls[0][1].body)).toEqual({ refreshToken: 'refresh-viejo' });
  });

  it('si le quedan menos de 60 segundos lo renueva antes de que venza', async () => {
    guardarToken(jwtQueVenceEn(30));
    pedido.mockResolvedValue(respuestaDeRenovacion('token-nuevo'));

    await expect(obtenerTokenVigente()).resolves.toBe('token-nuevo');
  });

  it('si la renovación falla (por ejemplo, sin red) devuelve el token que había', async () => {
    const viejo = jwtQueVenceEn(-10);
    guardarToken(viejo);
    pedido.mockRejectedValue(new Error('Failed to fetch'));

    await expect(obtenerTokenVigente()).resolves.toBe(viejo);
  });

  it('sin refresh token no intenta renovar', async () => {
    const viejo = jwtQueVenceEn(-10);
    guardarToken(viejo);
    guardarTokenRenovacion(null);

    await expect(obtenerTokenVigente()).resolves.toBe(viejo);
    expect(pedido).not.toHaveBeenCalled();
  });

  it('si el token no se puede leer lo devuelve sin renovar', async () => {
    guardarToken('no-es-un-jwt');

    await expect(obtenerTokenVigente()).resolves.toBe('no-es-un-jwt');
    expect(pedido).not.toHaveBeenCalled();
  });

  it('varias reconexiones a la vez comparten una sola renovación', async () => {
    guardarToken(jwtQueVenceEn(-10));
    pedido.mockResolvedValue(respuestaDeRenovacion('token-nuevo'));

    const tokens = await Promise.all([obtenerTokenVigente(), obtenerTokenVigente(), obtenerTokenVigente()]);

    expect(tokens).toEqual(['token-nuevo', 'token-nuevo', 'token-nuevo']);
    expect(pedido).toHaveBeenCalledTimes(1);
  });

  it('sin sesión devuelve texto vacío', async () => {
    guardarToken(null);
    await expect(obtenerTokenVigente()).resolves.toBe('');
  });
});
