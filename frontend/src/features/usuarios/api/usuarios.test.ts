import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { listarUsuarios, registrarUsuario } from './usuarios';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn() }));

describe('api de usuarios', () => {
  beforeEach(() => {
    vi.mocked(pedir).mockReset();
  });

  it.each(['Alumno', 'Docente'] as const)('registra al usuario con el rol %s', async (rol) => {
    vi.mocked(pedir).mockResolvedValue({ id: 'u-1', message: 'ok' });
    const datos = { nombre: 'Ana', apellido: 'Paz', email: 'ana@a.test', password: 'Clave1234', rol };

    await registrarUsuario(datos);

    expect(pedir).toHaveBeenCalledWith('/api/v1/identidad/auth/register', {
      method: 'POST',
      body: JSON.stringify(datos),
    });
  });

  it('lista los usuarios de un rol con nombre completo y email', async () => {
    vi.mocked(pedir).mockResolvedValue([
      { id: 'd-1', nombre: 'Marta', apellido: 'Díaz', email: 'marta@a.test', rol: 'Docente' },
    ]);

    const usuarios = await listarUsuarios('Docente');

    expect(pedir).toHaveBeenCalledWith('/api/v1/identidad/usuarios?rol=Docente');
    expect(usuarios).toEqual([{ id: 'd-1', nombre: 'Marta Díaz', email: 'marta@a.test', rol: 'Docente' }]);
  });
});
