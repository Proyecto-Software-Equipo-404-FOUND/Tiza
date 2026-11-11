import { describe, expect, it } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { mensajeApuntes } from './mensajesApuntes';

describe('mensajeApuntes', () => {
  it('muestra tal cual el title del servidor en un 415', () => {
    const error = new ApiError(415, { title: 'Solo se aceptan archivos PDF.', status: 415 });
    expect(mensajeApuntes(error)).toBe('Solo se aceptan archivos PDF.');
  });

  it('en un 400 usa el title y no el detail', () => {
    const error = new ApiError(400, {
      title: 'El PDF supera las 30 páginas permitidas.',
      status: 400,
      detail: 'texto que no debería mostrarse',
    });
    expect(mensajeApuntes(error)).toBe('El PDF supera las 30 páginas permitidas.');
  });

  it('muestra el title del servidor en un 403', () => {
    const error = new ApiError(403, {
      title: 'No tenés permiso para gestionar el material de este curso.',
      status: 403,
    });
    expect(mensajeApuntes(error)).toBe(
      'No tenés permiso para gestionar el material de este curso.',
    );
  });

  it('usa un texto claro si el 403 llega sin detalle', () => {
    expect(mensajeApuntes(new ApiError(403, null))).toBe(
      'No tenés permiso para gestionar el material de este curso.',
    );
  });

  it('usa un texto por defecto si el 404 llega sin title', () => {
    expect(mensajeApuntes(new ApiError(404, { status: 404 }))).toBe('No se encontró lo que buscás.');
  });

  it('delega en mensajeDeError para otros estados y errores no HTTP', () => {
    const e401 = new ApiError(401, { status: 401 });
    expect(mensajeApuntes(e401)).toBe(mensajeDeError(e401));
    const otro = new Error('boom');
    expect(mensajeApuntes(otro)).toBe(mensajeDeError(otro));
  });
});