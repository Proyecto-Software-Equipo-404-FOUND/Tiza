import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { importarAlumnos, listarUsuarios, registrarUsuario } from './usuarios';

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

  it('importa el CSV como multipart en el campo «archivo»', async () => {
    const resultado = { creados: 2, omitidos: 0, errores: [] };
    vi.mocked(pedir).mockResolvedValue(resultado);
    const archivo = new File(['nombre,apellido,email\nAna,Paz,ana@a.test'], 'alumnos.csv', { type: 'text/csv' });

    await expect(importarAlumnos(archivo)).resolves.toEqual(resultado);

    const [ruta, opciones] = vi.mocked(pedir).mock.calls[0] ?? [];
    expect(ruta).toBe('/api/v1/identidad/usuarios/alumnos/importacion');
    expect(opciones?.method).toBe('POST');
    expect(opciones?.body).toBeInstanceOf(FormData);
    expect((opciones?.body as FormData).get('archivo')).toBe(archivo);
  });

  it('envía la contraseña inicial solo si se indicó', async () => {
    vi.mocked(pedir).mockResolvedValue({ creados: 1, omitidos: 0, errores: [] });
    const archivo = new File(['nombre,apellido,email'], 'alumnos.csv', { type: 'text/csv' });

    await importarAlumnos(archivo, 'clave1234');
    await importarAlumnos(archivo, '');
    await importarAlumnos(archivo);

    const cuerpos = vi.mocked(pedir).mock.calls.map(([, opciones]) => opciones?.body as FormData);
    expect(cuerpos[0]?.get('contrasenaInicial')).toBe('clave1234');
    expect(cuerpos[1]?.has('contrasenaInicial')).toBe(false);
    expect(cuerpos[2]?.has('contrasenaInicial')).toBe(false);
  });
});
