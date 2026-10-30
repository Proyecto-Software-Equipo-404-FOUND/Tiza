import { beforeEach, describe, expect, it, vi } from 'vitest';
import { pedir } from '@/shared/api/cliente';
import { actualizarCohorte, eliminarCohorte } from './cohortes';
import { actualizarCurso, eliminarCurso } from './cursos';

vi.mock('@/shared/api/cliente', () => ({ pedir: vi.fn().mockResolvedValue(undefined) }));

beforeEach(() => {
  vi.mocked(pedir).mockClear();
});

describe('api de edición y baja', () => {
  it('actualiza un curso con PUT', async () => {
    await actualizarCurso('c1', { nombre: 'Nuevo', area: 'Sistemas' });
    expect(pedir).toHaveBeenCalledWith('/api/v1/cursos/c1', {
      method: 'PUT',
      body: JSON.stringify({ nombre: 'Nuevo', area: 'Sistemas' }),
    });
  });

  it('elimina un curso con DELETE', async () => {
    await eliminarCurso('c1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/cursos/c1', { method: 'DELETE' });
  });

  it('actualiza una cohorte con PUT en la ruta anidada del curso', async () => {
    const datos = { nombre: 'N', periodo: '2026-1C', docenteId: 'd1' };
    await actualizarCohorte('c1', 'h1', datos);
    expect(pedir).toHaveBeenCalledWith('/api/v1/cursos/c1/cohortes/h1', {
      method: 'PUT',
      body: JSON.stringify(datos),
    });
  });

  it('elimina una cohorte con DELETE en la ruta anidada del curso', async () => {
    await eliminarCohorte('c1', 'h1');
    expect(pedir).toHaveBeenCalledWith('/api/v1/cursos/c1/cohortes/h1', { method: 'DELETE' });
  });
});
