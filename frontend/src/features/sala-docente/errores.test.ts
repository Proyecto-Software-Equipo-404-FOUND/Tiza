import { describe, expect, it } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { codigoDeError, mensajeAbrirSala, mensajeAccionSala } from './errores';

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

describe('codigoDeError', () => {
  it('toma el código de un mensaje «CODIGO: mensaje»', () => {
    expect(codigoDeError(new Error('SALA_YA_INICIADA: La sala ya fue iniciada.'))).toBe('SALA_YA_INICIADA');
  });

  it('lo encuentra aunque venga con texto antes', () => {
    expect(
      codigoDeError(
        new Error("An unexpected error occurred invoking 'IniciarSala' on the server. SIN_PREGUNTAS: Sin preguntas."),
      ),
    ).toBe('SIN_PREGUNTAS');
  });

  it('un mensaje sin código o un corte de red cae en ERROR_INTERNO', () => {
    expect(codigoDeError(new Error('Alumno no identificado.'))).toBe('ERROR_INTERNO');
    expect(codigoDeError(new Error('Failed to fetch'))).toBe('ERROR_INTERNO');
    expect(codigoDeError(undefined)).toBe('ERROR_INTERNO');
  });
});

describe('mensajeAccionSala', () => {
  it('explica los códigos del docente', () => {
    expect(mensajeAccionSala(new Error('SIN_PREGUNTAS: x'))).toBe('La evaluación no tiene preguntas para tomar.');
    expect(mensajeAccionSala(new Error('NO_AUTORIZADO: x'))).toBe('No tenés permiso para conducir esta sala.');
  });

  it('un código desconocido o un corte usa el mensaje general', () => {
    expect(mensajeAccionSala(new Error('OTRO_CODIGO: x'))).toBe(
      'No pudimos completar la acción. Revisá tu conexión y probá de nuevo.',
    );
    expect(mensajeAccionSala(new Error('Failed to fetch'))).toBe(
      'No pudimos completar la acción. Revisá tu conexión y probá de nuevo.',
    );
  });
});