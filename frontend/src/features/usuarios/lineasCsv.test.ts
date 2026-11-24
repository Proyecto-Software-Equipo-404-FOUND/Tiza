import { describe, expect, it } from 'vitest';
import { leerTexto, lineasDelCsv } from './lineasCsv';

describe('lineasDelCsv', () => {
  it('separa por líneas, con la línea 1 en la posición 0', () => {
    expect(lineasDelCsv('nombre,apellido,email\r\nAna,Paz,ana@a.test\nLuis,Gómez,mal')).toEqual([
      'nombre,apellido,email',
      'Ana,Paz,ana@a.test',
      'Luis,Gómez,mal',
    ]);
  });

  it('ignora la marca BOM del principio', () => {
    expect(lineasDelCsv('\uFEFFnombre,apellido,email')[0]).toBe('nombre,apellido,email');
  });
});

describe('leerTexto', () => {
  it('lee el contenido del archivo', async () => {
    const archivo = new File(['nombre,apellido,email\nAna,Paz,ana@a.test'], 'a.csv', { type: 'text/csv' });
    await expect(leerTexto(archivo)).resolves.toBe('nombre,apellido,email\nAna,Paz,ana@a.test');
  });
});
