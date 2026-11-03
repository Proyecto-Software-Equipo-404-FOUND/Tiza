import { describe, expect, it } from 'vitest';
import { LIMITE_RECONEXION_MS, politicaReintentos } from './conexion';

const proximo = (previousRetryCount: number, elapsedMilliseconds = 0) =>
  politicaReintentos.nextRetryDelayInMilliseconds({
    previousRetryCount,
    elapsedMilliseconds,
    retryReason: new Error('corte'),
  });

describe('política de reconexión', () => {
  it('reintenta enseguida y después espera cada vez más', () => {
    expect([0, 1, 2, 3, 4, 5].map((n) => proximo(n))).toEqual([0, 2000, 5000, 10000, 15000, 30000]);
  });

  it('después del último valor sigue reintentando cada 30 segundos', () => {
    expect(proximo(6)).toBe(30000);
    expect(proximo(20)).toBe(30000);
  });

  it('insiste mucho más que los ~40 segundos del valor por defecto', () => {
    expect(proximo(7, 60_000)).toBe(30000);
  });

  it('se rinde pasado el límite de tiempo', () => {
    expect(proximo(8, LIMITE_RECONEXION_MS)).toBeNull();
    expect(proximo(8, LIMITE_RECONEXION_MS + 1)).toBeNull();
  });
});
