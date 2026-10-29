import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { PaginaSalaAlumno } from './PaginaSalaAlumno';
import type { EnvioPendiente, PreguntaActual, ResultadoEnvio } from './tipos';

const SALA = '11111111-1111-4111-8111-111111111111';
const P1 = '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60';
const P2 = '9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f';
const enSegundos = (s: number) => new Date(Date.now() + s * 1000).toISOString();

const pregunta = (cambios: Partial<PreguntaActual> = {}): PreguntaActual => ({
  salaId: SALA,
  preguntaId: P1,
  indice: 1,
  totalPreguntas: 2,
  tipo: 'OpcionMultiple',
  enunciado: '¿Cuánto es 2 + 3?',
  opciones: [
    { id: 'op-5', texto: '5' },
    { id: 'op-23', texto: '23' },
  ],
  puntajeMaximo: 2,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(300),
  ...cambios,
});

const resultado = (envio: EnvioPendiente, cambios: Partial<ResultadoEnvio> = {}): ResultadoEnvio => ({
  estado: 'Aceptada',
  preguntaId: envio.preguntaId,
  idempotencyKey: envio.idempotencyKey,
  secuencia: envio.secuencia,
  recibidaUtc: new Date().toISOString(),
  error: null,
  ...cambios,
});

type Envios = EnvioPendiente[];

function preparar(alEnviar: (envio: EnvioPendiente, intento: number) => Promise<ResultadoEnvio>) {
  const { conexion, simular } = crearConexionFalsa();
  const envios: Envios = [];
  conexion.invocar = vi.fn(async (metodo: string, ...args: unknown[]) => {
    if (metodo !== 'EnviarRespuesta') return undefined;
    const envio = args[0] as EnvioPendiente;
    envios.push(envio);
    return alEnviar(envio, envios.filter((e) => e.idempotencyKey === envio.idempotencyKey).length);
  }) as typeof conexion.invocar;
  render(<PaginaSalaAlumno conexion={conexion} esperar={async () => {}} />);
  return { conexion, simular, envios };
}

async function entrarYRecibir(simular: (e: string, ...a: unknown[]) => void, p: PreguntaActual = pregunta()) {
  await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled());
  await userEvent.type(screen.getByLabelText('PIN'), '123456');
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));
  await screen.findByRole('heading', { name: 'Estás en la sala' });
  act(() => {
    simular('salaIniciada', { salaId: SALA, iniciadaUtc: new Date().toISOString(), totalPreguntas: 2, fechaLimiteUtc: enSegundos(1800) });
    simular('preguntaActual', p);
  });
}

const enviar = () => userEvent.click(screen.getByRole('button', { name: 'Enviar respuesta' }));

describe('PaginaSalaAlumno · responder', () => {
  it('muestra la pregunta vigente cuando llega preguntaActual', async () => {
    const { simular } = preparar(async (e) => resultado(e));
    await entrarYRecibir(simular);
    expect(screen.getByText('Pregunta 1 de 2')).toBeInTheDocument();
    expect(screen.getByText('¿Cuánto es 2 + 3?')).toBeInTheDocument();
    expect(screen.getByRole('timer')).toBeInTheDocument();
  });

  it('envía la respuesta con clave y secuencia 1, y la marca como enviada', async () => {
    const { simular, envios } = preparar(async (e) => resultado(e));
    await entrarYRecibir(simular);

    await userEvent.click(screen.getByLabelText('5'));
    await enviar();

    expect(await screen.findByText('Tu respuesta fue enviada.')).toBeInTheDocument();
    expect(envios).toHaveLength(1);
    expect(envios[0]).toMatchObject({ preguntaId: P1, respuesta: 'op-5', secuencia: 1 });
    expect(envios[0].idempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('la segunda respuesta usa secuencia 2 y una clave nueva', async () => {
    const { simular, envios } = preparar(async (e) => resultado(e));
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();
    await screen.findByText('Tu respuesta fue enviada.');

    act(() => simular('preguntaActual', pregunta({ preguntaId: P2, indice: 2, tipo: 'Abierta', opciones: null })));
    await userEvent.type(await screen.findByLabelText('Tu respuesta'), 'Es un espacio con nombre');
    await enviar();

    await waitFor(() => expect(envios).toHaveLength(2));
    expect(envios[1]).toMatchObject({ preguntaId: P2, secuencia: 2, respuesta: 'Es un espacio con nombre' });
    expect(envios[1].idempotencyKey).not.toBe(envios[0].idempotencyKey);
  });

  it('si falla la red reintenta con la misma clave y termina aceptada', async () => {
    const { simular, envios } = preparar(async (e, intento) => {
      if (intento === 1) throw new Error('Failed to fetch');
      return resultado(e, { estado: 'Duplicada' });
    });
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();

    expect(await screen.findByText('Tu respuesta fue enviada.')).toBeInTheDocument();
    expect(envios).toHaveLength(2);
    expect(envios[1].idempotencyKey).toBe(envios[0].idempotencyKey);
    expect(envios[1].secuencia).toBe(envios[0].secuencia);
  });

  it('si se agotan los reintentos avisa y el reintento manual conserva la misma clave', async () => {
    let caida = true;
    const { simular, envios } = preparar(async (e) => {
      if (caida) throw new Error('Failed to fetch');
      return resultado(e);
    });
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();

    expect(await screen.findByText(/No pudimos enviar tu respuesta/)).toBeInTheDocument();
    expect(envios).toHaveLength(3);

    caida = false;
    await enviar();
    expect(await screen.findByText('Tu respuesta fue enviada.')).toBeInTheDocument();
    expect(new Set(envios.map((e) => e.idempotencyKey)).size).toBe(1);
  });

  it('muestra el mensaje de una respuesta rechazada y deja volver a intentar', async () => {
    const { simular } = preparar(async (e) =>
      resultado(e, { estado: 'Rechazada', error: { codigo: 'RESPUESTA_INVALIDA', mensaje: 'x', reintentable: false } }),
    );
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();

    expect(await screen.findByText('Revisá tu respuesta e intentá de nuevo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeEnabled();
  });

  it('«ya registrada» se toma como respondida', async () => {
    const { simular } = preparar(async (e) =>
      resultado(e, { estado: 'Rechazada', error: { codigo: 'RESPUESTA_YA_REGISTRADA', mensaje: 'x', reintentable: false } }),
    );
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();
    expect(await screen.findByText('Tu respuesta fue enviada.')).toBeInTheDocument();
  });

  it('con TIEMPO_AGOTADO muestra el aviso', async () => {
    const { simular } = preparar(async (e) =>
      resultado(e, { estado: 'Rechazada', error: { codigo: 'TIEMPO_AGOTADO', mensaje: 'x', reintentable: false } }),
    );
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();
    expect(await screen.findByText('Se terminó el tiempo para responder.')).toBeInTheDocument();
  });

  it('con PREGUNTA_NO_VIGENTE pide el estado actual al servidor', async () => {
    const { simular, conexion } = preparar(async (e) =>
      resultado(e, { estado: 'Rechazada', error: { codigo: 'PREGUNTA_NO_VIGENTE', mensaje: 'x', reintentable: false } }),
    );
    await entrarYRecibir(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await enviar();

    expect(await screen.findByText('La pregunta cambió. Mirá la pregunta actual.')).toBeInTheDocument();
    expect(conexion.invocar).toHaveBeenCalledWith('ObtenerEstadoSala');
  });

  it('al reconectar recupera la pregunta, lo ya respondido y la secuencia', async () => {
    const { simular, envios } = preparar(async (e) => resultado(e));
    await entrarYRecibir(simular);

    act(() =>
      simular('estadoSala', {
        salaId: SALA,
        estado: 'EnCurso',
        fechaLimiteUtc: enSegundos(1800),
        preguntaActual: pregunta({ preguntaId: P2, indice: 2, tipo: 'Abierta', opciones: null }),
        ultimaSecuencia: 1,
        preguntasRespondidas: [P1],
        servidorUtc: new Date().toISOString(),
      }),
    );

    await userEvent.type(await screen.findByLabelText('Tu respuesta'), 'hola');
    await enviar();
    await waitFor(() => expect(envios).toHaveLength(1));
    expect(envios[0].secuencia).toBe(2);
  });

  it('al finalizar la sala deja de mostrar la pregunta', async () => {
    const { simular } = preparar(async (e) => resultado(e));
    await entrarYRecibir(simular);
    act(() => simular('salaFinalizada', { salaId: SALA, finalizadaUtc: new Date().toISOString(), motivo: 'Docente' }));
    expect(screen.getByRole('heading', { name: 'La sala finalizó' })).toBeInTheDocument();
    expect(screen.queryByText('¿Cuánto es 2 + 3?')).not.toBeInTheDocument();
  });
});
