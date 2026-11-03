import { describe, expect, it } from 'vitest';
import { codigoDeError, mensajeDeCodigo } from './errores';

describe('codigoDeError', () => {
  it('reconoce el código cuando el servidor usa «CODIGO: mensaje»', () => {
    expect(codigoDeError(new Error('SALA_LLENA: la sala alcanzó su capacidad máxima.'))).toBe('SALA_LLENA');
  });

  it('reconoce el código aunque SignalR anteponga texto', () => {
    const error = new Error(
      "An unexpected error occurred invoking 'UnirseASala' on the server. HubException: SALA_FINALIZADA: ya cerró.",
    );
    expect(codigoDeError(error)).toBe('SALA_FINALIZADA');
  });

  it.each([
    ['Alumno no identificado.', 'NO_AUTENTICADO'],
    ['Institución no válida.', 'NO_AUTORIZADO'],
    ['No tienes permiso para ingresar a esta sala.', 'SALA_OTRA_INSTITUCION'],
    ['La sala no existe, está inactiva o venció.', 'SALA_NO_ENCONTRADA'],
    ['El alumno ya está registrado en la sala.', 'ALUMNO_YA_EN_SALA'],
    ['La sala ha alcanzado su capacidad máxima.', 'SALA_LLENA'],
  ])('traduce el mensaje actual del hub «%s»', (mensaje, esperado) => {
    expect(codigoDeError(new Error(mensaje))).toBe(esperado);
  });

  it('usa ERROR_INTERNO para lo desconocido y para valores que no son Error', () => {
    expect(codigoDeError(new Error('algo raro'))).toBe('ERROR_INTERNO');
    expect(codigoDeError('texto suelto')).toBe('ERROR_INTERNO');
  });
});

describe('mensajeDeCodigo', () => {
  it('no revela por qué una sala es de otra institución', () => {
    expect(mensajeDeCodigo('SALA_OTRA_INSTITUCION')).toBe('No podés entrar a esta sala.');
  });
});
