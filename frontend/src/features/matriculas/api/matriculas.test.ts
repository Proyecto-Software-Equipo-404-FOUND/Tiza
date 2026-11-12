import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { listarAlumnos, listarMatriculas } from './matriculas';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn() }));

describe('listarAlumnos', () => {
  beforeEach(() => {
    vi.mocked(pedir).mockReset();
  });

  it('pide los usuarios con rol Alumno y arma el nombre completo', async () => {
    vi.mocked(pedir).mockResolvedValue([
      { id: 'a-1', nombre: 'Ana', apellido: 'Pérez', email: 'ana@a.test', rol: 'Alumno' },
      { id: 'a-2', nombre: 'Luis', apellido: '', email: 'luis@a.test', rol: 'Alumno' },
    ]);

    const alumnos = await listarAlumnos();

    expect(pedir).toHaveBeenCalledWith('/api/v1/identidad/usuarios?rol=Alumno');
    expect(alumnos).toEqual([
      { id: 'a-1', nombre: 'Ana Pérez' },
      { id: 'a-2', nombre: 'Luis' },
    ]);
  });
});


describe('listarMatriculas', () => {
  it('pide también las dadas de baja para poder mostrarlas aparte', async () => {
    vi.mocked(pedir).mockResolvedValue([]);
    await listarMatriculas('c-1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/cohortes/c-1/matriculas?soloActivas=false');
  });
});
