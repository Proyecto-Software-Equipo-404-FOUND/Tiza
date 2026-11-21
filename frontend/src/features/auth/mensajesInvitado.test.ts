import { ApiError } from '@/shared/api/cliente';
import { mensajeIngresoInvitado } from './mensajesInvitado';

const falla = (estado: number, type?: string) => new ApiError(estado, { status: estado, type });

describe('mensajeIngresoInvitado', () => {
  it.each([
    [400, 'NOMBRE_INVALIDO', 'El nombre tiene que tener entre 2 y 60 caracteres.'],
    [403, 'SALA_SIN_INVITADOS', 'Esta sala no admite invitados. Pedile al docente que te habilite.'],
    [404, 'SALA_NO_ENCONTRADA', 'No hay una sala activa con ese PIN. Revisá el número e intentá de nuevo.'],
    [409, 'SALA_FINALIZADA', 'La sala ya finalizó.'],
    [503, 'SALA_NO_DISPONIBLE', 'No pudimos verificar la sala. Probá de nuevo.'],
  ])('con el código de un %s (%s) muestra el mensaje del ingreso', (estado, codigo, texto) => {
    expect(mensajeIngresoInvitado(falla(estado, codigo))).toBe(texto);
  });

  it('un 503 sin código también avisa que no se pudo verificar la sala', () => {
    expect(mensajeIngresoInvitado(falla(503))).toBe('No pudimos verificar la sala. Probá de nuevo.');
  });

  it('sin código conocido usa el mensaje genérico del estado', () => {
    expect(mensajeIngresoInvitado(falla(404))).toBe('No se encontró lo que buscás.');
    expect(mensajeIngresoInvitado(falla(404, 'OTRO_CODIGO'))).toBe('No se encontró lo que buscás.');
  });

  it('un error que no es de la API muestra el mensaje genérico', () => {
    expect(mensajeIngresoInvitado(new Error('falló'))).toBe('Algo salió mal. Probá de nuevo.');
  });
});