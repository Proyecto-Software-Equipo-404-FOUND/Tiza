import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { listarEvaluaciones, obtenerEvaluacion } from './evaluaciones';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn().mockResolvedValue([]) }));

beforeEach(() => {
  vi.mocked(pedir).mockClear();
});

describe('api de evaluaciones', () => {
  it('lista las evaluaciones enviando la institución en X-Institucion-Id', async () => {
    await listarEvaluaciones('inst-1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/evaluaciones', {
      headers: { 'X-Institucion-Id': 'inst-1' },
    });
  });

  it('pide el detalle por id con la misma cabecera', async () => {
    await obtenerEvaluacion('e1', 'inst-1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/evaluaciones/e1', {
      headers: { 'X-Institucion-Id': 'inst-1' },
    });
  });
});
