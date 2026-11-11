import { describe, expect, it } from 'vitest';
import { formatearTamano, TAMANO_MAXIMO_BYTES, validarArchivo } from './apuntes';

const archivo = (name: string, type: string, size: number) => ({ name, type, size });

describe('validarArchivo', () => {
  it('acepta un PDF de tamaño razonable', () => {
    expect(validarArchivo(archivo('u1.pdf', 'application/pdf', 1000))).toBeNull();
  });

  it('acepta un .pdf aunque el navegador no informe el tipo', () => {
    expect(validarArchivo(archivo('U1.PDF', '', 1000))).toBeNull();
  });

  it('rechaza lo que no es PDF', () => {
    expect(validarArchivo(archivo('foto.png', 'image/png', 1000))).toBe('Solo se aceptan archivos PDF.');
  });

  it('rechaza un archivo vacío y uno de más de 10 MB', () => {
    expect(validarArchivo(archivo('a.pdf', 'application/pdf', 0))).toBe('El archivo está vacío.');
    expect(validarArchivo(archivo('a.pdf', 'application/pdf', TAMANO_MAXIMO_BYTES + 1))).toBe('El PDF pesa más de 10 MB.');
    expect(validarArchivo(archivo('a.pdf', 'application/pdf', TAMANO_MAXIMO_BYTES))).toBeNull();
  });
});

describe('formatearTamano', () => {
  it('elige la unidad según el tamaño', () => {
    expect(formatearTamano(500)).toBe('500 B');
    expect(formatearTamano(1843200)).toBe('1.8 MB');
    expect(formatearTamano(20480)).toBe('20 KB');
  });
});