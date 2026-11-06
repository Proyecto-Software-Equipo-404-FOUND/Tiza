import { describe, expect, it } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { mensajeAbrirSala } from './errores';

const error = (estado: number, title?: string) => new ApiError(estado, { status: estado, title });

describe('mensajeAbrirSala', () => {
  it('en un 400 muestra el título que manda el servidor', () => {
    expect(mensajeAbrirSala(error(400, 'La capacidad máxima debe estar entre 1 y 100.'))).toBe(
      'La capacidad máxima debe estar entre 1 y 100.',
    );
  });

  it('en un 400 sin título usa un texto propio', () => {
    expect(mensajeAbrirSala(new ApiError(400, null))).toBe('Revisá la capacidad: tiene que estar entre 1 y 100.');
  });

  it('explica el 403, el 404 y el 503', () => {
    expect(mensajeAbrirSala(error(403))).toBe('Solo un docente o administrador puede abrir una sala.');
    expect(mensajeAbrirSala(error(404))).toBe(
      'Esta evaluación no existe, no está publicada o es de otra institución.',
    );
    expect(mensajeAbrirSala(error(503))).toBe('No se pudo abrir la sala. Intentá de nuevo.');
  });

  it('lo que no reconoce cae en el mensaje general', () => {
    expect(mensajeAbrirSala(error(401))).toBe('Tu sesión venció. Ingresá de nuevo.');
    expect(mensajeAbrirSala(error(500))).toBe('Algo salió mal. Probá de nuevo.');
    expect(mensajeAbrirSala(new Error('Failed to fetch'))).toBe('Algo salió mal. Probá de nuevo.');
  });
});