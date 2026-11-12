import { describe, expect, it } from 'vitest';
import type { Matricula } from './api/matriculas';
import { fechaCorta, nombreDeAlumno } from './nombres';

const matricula = (cambios: Partial<Matricula> = {}): Matricula => ({
  id: 'm1',
  cohorteId: 'c1',
  alumnoId: '11111111-0000-0000-0000-000000000003',
  estado: 'Activa',
  ...cambios,
});

describe('nombreDeAlumno', () => {
  it('usa el nombre de la lista de alumnos cuando el alumno está en ella', () => {
    const alumnos = [{ id: '11111111-0000-0000-0000-000000000003', nombre: 'Alumno Uno' }];
    expect(nombreDeAlumno(matricula(), alumnos)).toBe('Alumno Uno');
  });

  it('si no está en la lista usa el nombre que haya traído la matrícula', () => {
    expect(nombreDeAlumno(matricula({ alumnoNombre: 'Ana' }), [])).toBe('Ana');
  });

  it('si no hay ningún nombre muestra una referencia corta, no el id entero', () => {
    expect(nombreDeAlumno(matricula(), [])).toBe('Alumno …000003');
  });
});

describe('fechaCorta', () => {
  it('formatea una fecha ISO en día/mes/año', () => {
    expect(fechaCorta('2026-10-31T12:00:00Z')).toBe('31/10/2026');
  });

  it('devuelve null si falta o no es válida', () => {
    expect(fechaCorta(undefined)).toBeNull();
    expect(fechaCorta('no es una fecha')).toBeNull();
  });
});
