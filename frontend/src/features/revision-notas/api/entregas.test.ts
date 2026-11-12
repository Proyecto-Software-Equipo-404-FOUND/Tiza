import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { listarEntregas, obtenerEntrega } from './entregas';
import { listarAlumnos } from './usuarios';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn().mockResolvedValue([]) }));

beforeEach(() => {
  vi.mocked(pedir).mockClear();
});

describe('api de resultados', () => {
  it('lista las entregas de una evaluación enviando la institución', async () => {
    await listarEntregas('e1', 'inst-1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/evaluaciones/e1/entregas', {
      headers: { 'X-Institucion-Id': 'inst-1' },
    });
  });

  it('pide el detalle de una entrega', async () => {
    await obtenerEntrega('e1', 'en1', 'inst-1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/evaluaciones/e1/entregas/en1', {
      headers: { 'X-Institucion-Id': 'inst-1' },
    });
  });

  it('pide los alumnos a Identidad por el gateway', async () => {
    await listarAlumnos();
    expect(pedir).toHaveBeenCalledWith('/api/v1/identidad/usuarios?rol=Alumno');
  });
});
