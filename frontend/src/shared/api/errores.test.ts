import { ApiError } from './cliente';
import { mensajeDeError } from './errores';

describe('mensajeDeError', () => {
  it('devuelve el texto de conflicto para un 409', () => {
    const error = new ApiError(409, { title: 'Conflicto', status: 409 });
    expect(mensajeDeError(error)).toBe(
      'Alguien modificó este dato. Recargá la página e intentá de nuevo.',
    );
  });

  it('devuelve el texto de permiso para un 403', () => {
    expect(mensajeDeError(new ApiError(403, null))).toBe('No tenés permiso para hacer esto.');
  });

  it('devuelve el detalle del servidor para un 400', () => {
    const error = new ApiError(400, { status: 400, detail: 'El nombre es obligatorio.' });
    expect(mensajeDeError(error)).toBe('El nombre es obligatorio.');
  });

  it('devuelve un texto genérico para errores desconocidos', () => {
    expect(mensajeDeError(new Error('x'))).toBe('Algo salió mal. Probá de nuevo.');
  });
});
