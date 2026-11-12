import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { listarDocentes } from './docentes';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn() }));

describe('listarDocentes', () => {
  beforeEach(() => {
    vi.mocked(pedir).mockReset();
  });

  it('pide los usuarios con rol Docente y arma el nombre completo', async () => {
    vi.mocked(pedir).mockResolvedValue([
      { id: 'd-1', nombre: 'Marta', apellido: 'Díaz', email: 'marta@a.test', rol: 'Docente' },
    ]);

    const docentes = await listarDocentes();

    expect(pedir).toHaveBeenCalledWith('/api/v1/identidad/usuarios?rol=Docente');
    expect(docentes).toEqual([{ id: 'd-1', nombre: 'Marta Díaz' }]);
  });
});
