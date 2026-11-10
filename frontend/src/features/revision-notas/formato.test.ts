import { describe, expect, it } from 'vitest';
import {
  esRoja,
  formatearDuracion,
  formatearNota,
  formatearNumero,
  nombreCompleto,
  nombreDeReemplazo,
} from './formato';

describe('formato de resultados', () => {
  it('usa la coma decimal y no agrega ceros', () => {
    expect(formatearNumero(3.5)).toBe('3,5');
    expect(formatearNumero(4)).toBe('4');
  });

  it('arma «nota / máximo» y devuelve null si no hay nota', () => {
    expect(formatearNota(3.5, 4)).toBe('3,5 / 4');
    expect(formatearNota(0, 2)).toBe('0 / 2');
    expect(formatearNota(null, 4)).toBeNull();
    expect(formatearNota(undefined, 4)).toBeNull();
    expect(formatearNota(2, null)).toBe('2');
  });

  it('formatea la duración fuera de la pantalla', () => {
    expect(formatearDuracion(800)).toBe('menos de 1 s');
    expect(formatearDuracion(4200)).toBe('4 s');
    expect(formatearDuracion(60000)).toBe('1 min');
    expect(formatearDuracion(65000)).toBe('1 min 5 s');
  });

  it('reconoce la severidad roja sin depender de mayúsculas', () => {
    expect(esRoja({ severidad: 'Roja' })).toBe(true);
    expect(esRoja({ severidad: 'roja' })).toBe(true);
    expect(esRoja({ severidad: 'Amarillo' })).toBe(false);
  });

  it('muestra «Apellido, Nombre» y un reemplazo corto sin nombre', () => {
    expect(nombreCompleto({ nombre: 'Ana', apellido: 'Pérez' })).toBe('Pérez, Ana');
    expect(nombreDeReemplazo('11111111-0000-0000-0000-000000000099')).toBe('Alumno ···0099');
  });
});
