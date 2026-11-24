import { ApiError } from './cliente';
import { mensajeDeError } from './errores';

describe('mensajeDeError', () => {
  it('devuelve el texto de conflicto para un 409 sin detalle', () => {
    const error = new ApiError(409, { title: 'Conflicto', status: 409 });
    expect(mensajeDeError(error)).toBe(
      'Alguien modificó este dato. Recargá la página e intentá de nuevo.',
    );
  });

  it('devuelve el detalle del servidor para un 409 con detail', () => {
    const error = new ApiError(409, {
      title: 'Conflicto en la operación',
      status: 409,
      detail:
        "Ya existe una comision llamada 'Cohorte Prueba' para el ciclo '2026-2C' en este curso.",
    });
    expect(mensajeDeError(error)).toBe(
      "Ya existe una comision llamada 'Cohorte Prueba' para el ciclo '2026-2C' en este curso.",
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

  it('avisa que la sesión venció ante un 401', () => {
    expect(mensajeDeError(new ApiError(401, null))).toBe('Tu sesión venció. Ingresá de nuevo.');
  });

  it('avisa del límite de intentos ante un 429', () => {
    expect(mensajeDeError(new ApiError(429, null))).toBe(
      'Demasiados intentos. Esperá un momento e intentá de nuevo.',
    );
  });

  it('explica por qué no se puede eliminar un curso con alumnos (CURSO_CON_ALUMNOS)', () => {
    const error = new ApiError(409, {
      title: 'No se puede desactivar un curso con alumnos matriculados.',
      status: 409,
      type: 'CURSO_CON_ALUMNOS',
    });
    expect(mensajeDeError(error)).toBe(
      'No se puede eliminar el curso porque tiene alumnos matriculados. Primero dá de baja sus matrículas.',
    );
  });

  it('explica el conflicto de comisión (ALUMNO_EN_OTRA_COMISION)', () => {
    const error = new ApiError(409, { title: 'x', status: 409, type: 'ALUMNO_EN_OTRA_COMISION' });
    expect(mensajeDeError(error)).toBe(
      'Este alumno ya está en otra comisión de este curso. Dalo de baja de esa comisión para matricularlo acá.',
    );
  });

  it('ALUMNO_EN_OTRA_COMISION nombra la comisión actual si el servidor la informa', () => {
    const error = new ApiError(409, {
      title: 'x',
      status: 409,
      type: 'ALUMNO_EN_OTRA_COMISION',
      cohorteActualId: 'c1',
      cohorteActualNombre: 'Comisión B',
    });
    expect(mensajeDeError(error)).toBe(
      'Este alumno ya está en la comisión «Comisión B» de este curso. Dalo de baja de esa comisión para matricularlo acá.',
    );
  });

  it('ALUMNO_EN_OTRA_COMISION con el nombre vacío o null usa el mensaje general', () => {
    const error = new ApiError(409, {
      title: 'x',
      status: 409,
      type: 'ALUMNO_EN_OTRA_COMISION',
      cohorteActualNombre: null,
    });
    expect(mensajeDeError(error)).toBe(
      'Este alumno ya está en otra comisión de este curso. Dalo de baja de esa comisión para matricularlo acá.',
    );
  });

  it('con un código que no conoce, sigue con el mensaje según el estado', () => {
    const error = new ApiError(409, { title: 'x', status: 409, type: 'OTRO_CODIGO' });
    expect(mensajeDeError(error)).toBe('Alguien modificó este dato. Recargá la página e intentá de nuevo.');
  });
});
