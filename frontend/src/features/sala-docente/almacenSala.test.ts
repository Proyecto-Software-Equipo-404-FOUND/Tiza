import { beforeEach, describe, expect, it } from 'vitest';
import { guardarSala, olvidarSala, recuperarSala } from './almacenSala';
import type { SalaCreada } from './tipos';

const AHORA = Date.parse('2026-11-05T14:00:00.000Z');

const sala = (cambios: Partial<SalaCreada> = {}): SalaCreada => ({
  id: '6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9',
  codigoPIN: 482913,
  fechaVencimiento: '2026-11-05T16:00:00.000Z',
  capacidadMaxima: 30,
  ...cambios,
});

describe('almacenSala', () => {
  beforeEach(() => sessionStorage.clear());

  it('recupera la sala guardada por el mismo usuario', () => {
    guardarSala('ev-1', 'doc-1', sala());
    expect(recuperarSala('ev-1', 'doc-1', AHORA)).toEqual(sala());
  });

  it('cada evaluación tiene su propia sala', () => {
    guardarSala('ev-1', 'doc-1', sala());
    expect(recuperarSala('ev-2', 'doc-1', AHORA)).toBeNull();
  });

  it('no entrega la sala de otro usuario y la descarta', () => {
    guardarSala('ev-1', 'doc-1', sala());
    expect(recuperarSala('ev-1', 'doc-2', AHORA)).toBeNull();
    expect(recuperarSala('ev-1', 'doc-1', AHORA)).toBeNull();
  });

  it('descarta la sala vencida', () => {
    guardarSala('ev-1', 'doc-1', sala({ fechaVencimiento: '2026-11-05T13:59:59.000Z' }));
    expect(recuperarSala('ev-1', 'doc-1', AHORA)).toBeNull();
    expect(sessionStorage.getItem('sala-docente:ev-1')).toBeNull();
  });

  it('descarta datos rotos o incompletos sin lanzar error', () => {
    sessionStorage.setItem('sala-docente:ev-1', 'no es json');
    expect(recuperarSala('ev-1', 'doc-1', AHORA)).toBeNull();
    sessionStorage.setItem('sala-docente:ev-1', JSON.stringify({ usuarioId: 'doc-1', sala: { id: 'x' } }));
    expect(recuperarSala('ev-1', 'doc-1', AHORA)).toBeNull();
  });

  it('olvidarSala borra lo guardado', () => {
    guardarSala('ev-1', 'doc-1', sala());
    olvidarSala('ev-1');
    expect(recuperarSala('ev-1', 'doc-1', AHORA)).toBeNull();
  });
});