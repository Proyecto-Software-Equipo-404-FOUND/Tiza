import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { actualizarEvaluacion, crearEvaluacion } from './evaluaciones';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn().mockResolvedValue({}) }));

const datos = { titulo: 'Parcial', tipo: 'Examen', preguntasJson: { version: 1, preguntas: [] } };

beforeEach(() => {
  vi.mocked(pedir).mockClear();
});

describe('api de evaluaciones (escritura)', () => {
  it('crea con POST, el cuerpo del contrato y X-Institucion-Id', async () => {
    await crearEvaluacion('inst-1', datos);
    expect(pedir).toHaveBeenCalledWith('/api/v1/evaluaciones', {
      headers: { 'X-Institucion-Id': 'inst-1' },
      method: 'POST',
      body: JSON.stringify(datos),
    });
  });

  it('actualiza con PUT sobre el id y con la misma cabecera', async () => {
    await actualizarEvaluacion('e1', 'inst-1', datos);
    expect(pedir).toHaveBeenCalledWith('/api/v1/evaluaciones/e1', {
      headers: { 'X-Institucion-Id': 'inst-1' },
      method: 'PUT',
      body: JSON.stringify(datos),
    });
  });
});
