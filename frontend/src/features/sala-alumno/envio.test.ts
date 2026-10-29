import { describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { enviarConReintentos, MAX_INTENTOS, nuevaClave } from './envio';
import type { EnvioPendiente, ResultadoEnvio } from './tipos';

const envio: EnvioPendiente = {
  preguntaId: '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60',
  respuesta: 'abc',
  idempotencyKey: '0b1c2d3e-4f50-4a61-8b72-93a4b5c6d7e8',
  secuencia: 1,
};
const aceptada: ResultadoEnvio = { estado: 'Aceptada', preguntaId: envio.preguntaId, idempotencyKey: envio.idempotencyKey, secuencia: 1, recibidaUtc: '2026-10-28T14:00:00.000Z', error: null };
const sinEsperar = vi.fn(async () => {});

function conexionCon(invocar: (...args: unknown[]) => Promise<unknown>) {
  const { conexion } = crearConexionFalsa();
  conexion.invocar = vi.fn(invocar) as typeof conexion.invocar;
  return conexion;
}

describe('nuevaClave', () => {
  it('genera UUID v4 distintos', () => {
    const a = nuevaClave();
    expect(a).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(nuevaClave()).not.toBe(a);
  });
});

describe('enviarConReintentos', () => {
  it('envía el payload del contrato y devuelve el resultado', async () => {
    const conexion = conexionCon(async () => aceptada);
    await expect(enviarConReintentos(conexion, envio, sinEsperar)).resolves.toEqual(aceptada);
    expect(conexion.invocar).toHaveBeenCalledWith('EnviarRespuesta', {
      preguntaId: envio.preguntaId,
      respuesta: 'abc',
      idempotencyKey: envio.idempotencyKey,
      secuencia: 1,
    });
  });

  it('reintenta tras un error de red con la misma clave y secuencia', async () => {
    let llamadas = 0;
    const conexion = conexionCon(async () => {
      if (++llamadas === 1) throw new Error('Failed to fetch');
      return aceptada;
    });
    await expect(enviarConReintentos(conexion, envio, sinEsperar)).resolves.toEqual(aceptada);

    const cargas = vi.mocked(conexion.invocar).mock.calls.map((llamada) => llamada[1] as EnvioPendiente);
    expect(cargas).toHaveLength(2);
    expect(cargas[1].idempotencyKey).toBe(cargas[0].idempotencyKey);
    expect(cargas[1].secuencia).toBe(cargas[0].secuencia);
  });

  it('reintenta un rechazo reintentable', async () => {
    const rechazo: ResultadoEnvio = { ...aceptada, estado: 'Rechazada', error: { codigo: 'ERROR_INTERNO', mensaje: 'x', reintentable: true } };
    const respuestas = [rechazo, aceptada];
    const conexion = conexionCon(async () => respuestas.shift());
    await expect(enviarConReintentos(conexion, envio, sinEsperar)).resolves.toEqual(aceptada);
    expect(conexion.invocar).toHaveBeenCalledTimes(2);
  });

  it('no reintenta un rechazo definitivo', async () => {
    const rechazo: ResultadoEnvio = { ...aceptada, estado: 'Rechazada', error: { codigo: 'TIEMPO_AGOTADO', mensaje: 'x', reintentable: false } };
    const conexion = conexionCon(async () => rechazo);
    await expect(enviarConReintentos(conexion, envio, sinEsperar)).resolves.toEqual(rechazo);
    expect(conexion.invocar).toHaveBeenCalledTimes(1);
  });

  it('lanza el error si se agotan los intentos por red', async () => {
    const conexion = conexionCon(async () => {
      throw new Error('Failed to fetch');
    });
    await expect(enviarConReintentos(conexion, envio, sinEsperar)).rejects.toThrow('Failed to fetch');
    expect(conexion.invocar).toHaveBeenCalledTimes(MAX_INTENTOS);
  });
});
