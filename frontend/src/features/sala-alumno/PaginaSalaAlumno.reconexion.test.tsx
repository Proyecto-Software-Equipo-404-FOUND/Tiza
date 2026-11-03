import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { PaginaSalaAlumno } from './PaginaSalaAlumno';
import type { EnvioPendiente, EstadoSalaEvento, PreguntaActual, ResultadoEnvio } from './tipos';

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

const estado = (cambios: Partial<EstadoSalaEvento> = {}): EstadoSalaEvento => ({
  salaId: SALA,
  estado: 'EnCurso',
  fechaLimiteUtc: enSegundos(1800),
  preguntaActual: pregunta(),
  ultimaSecuencia: 0,
  preguntasRespondidas: [],
  servidorUtc: new Date().toISOString(),
  ...cambios,
});

const aceptada = (envio: EnvioPendiente): ResultadoEnvio => ({
  estado: 'Aceptada',
  preguntaId: envio.preguntaId,
  idempotencyKey: envio.idempotencyKey,
  secuencia: envio.secuencia,
  recibidaUtc: new Date().toISOString(),
  error: null,
});

type Invocar = (metodo: string, simular: (e: string, ...a: unknown[]) => void, ...args: unknown[]) => Promise<unknown>;

function preparar(invocar: Invocar) {
  const { conexion, simular, simularEstado } = crearConexionFalsa();
  const llamadas: Array<{ metodo: string; args: unknown[] }> = [];
  conexion.invocar = vi.fn(async (metodo: string, ...args: unknown[]) => {
    llamadas.push({ metodo, args });
    return invocar(metodo, (e, ...a) => act(() => simular(e, ...a)), ...args);
  }) as typeof conexion.invocar;
  render(<PaginaSalaAlumno conexion={conexion} esperar={async () => {}} />);
  const cortarYVolver = () => {
    act(() => simularEstado('reconectando'));
    act(() => simularEstado('conectado'));
  };
  const pedidosDeEstado = () => llamadas.filter((l) => l.metodo === 'ObtenerEstadoSala').length;
  const envios = () => llamadas.filter((l) => l.metodo === 'EnviarRespuesta').map((l) => l.args[0] as EnvioPendiente);
  return { simular, simularEstado, cortarYVolver, pedidosDeEstado, envios };
}

async function entrar(simular: (e: string, ...a: unknown[]) => void) {
  await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled());
  await userEvent.type(screen.getByLabelText('PIN'), '123456');
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));
  await screen.findByRole('heading', { name: 'Estás en la sala' });
  act(() => {
    simular('salaIniciada', { salaId: SALA, iniciadaUtc: new Date().toISOString(), totalPreguntas: 2, fechaLimiteUtc: enSegundos(1800) });
    simular('preguntaActual', pregunta());
  });
  await screen.findByText('Pregunta 1 de 2');
}

describe('PaginaSalaAlumno · reconexión', () => {
  it('al volver la conexión pide el estado de la sala y muestra la pregunta vigente', async () => {
    const { simular, cortarYVolver, pedidosDeEstado } = preparar(async (metodo, emitir) => {
      if (metodo === 'ObtenerEstadoSala') {
        emitir('estadoSala', estado({ preguntaActual: pregunta({ preguntaId: P2, indice: 2 }) }));
      }
      return undefined;
    });
    await entrar(simular);

    cortarYVolver();

    expect(await screen.findByText('Pregunta 2 de 2')).toBeInTheDocument();
    expect(pedidosDeEstado()).toBe(1);
  });

  it('mientras no hay conexión no deja enviar, pero conserva lo escrito', async () => {
    const { simular, simularEstado } = preparar(async () => undefined);
    await entrar(simular);
    await userEvent.click(screen.getByLabelText('5'));

    act(() => simularEstado('reconectando'));

    expect(screen.getByRole('alert')).toHaveTextContent('Se cortó la conexión. Reconectando…');
    expect(screen.getByText(/Sin conexión/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeDisabled();
    expect(screen.getByLabelText('5')).toBeChecked();

    act(() => simularEstado('conectado'));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeEnabled());
  });

  it('un corte antes de entrar con el PIN no pide nada al servidor', async () => {
    const { cortarYVolver, pedidosDeEstado } = preparar(async () => undefined);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled());

    cortarYVolver();

    await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled());
    expect(pedidosDeEstado()).toBe(0);
  });

  it('reenvía con la misma clave y secuencia la respuesta que quedó sin acuse', async () => {
    let enviosRecibidos = 0;
    const { simular, cortarYVolver, envios } = preparar(async (metodo, emitir, ...args) => {
      if (metodo === 'EnviarRespuesta') {
        enviosRecibidos += 1;
        // Los 3 intentos seguidos fallan por red; el cuarto (ya reconectado) llega
        if (enviosRecibidos <= 3) throw new Error('Failed to fetch');
        return aceptada(args[0] as EnvioPendiente);
      }
      if (metodo === 'ObtenerEstadoSala') emitir('estadoSala', estado());
      return undefined;
    });
    await entrar(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await userEvent.click(screen.getByRole('button', { name: 'Enviar respuesta' }));
    await screen.findByText(/No pudimos enviar tu respuesta/);

    cortarYVolver();

    expect(await screen.findByText('Tu respuesta fue enviada.')).toBeInTheDocument();
    const todos = envios();
    expect(todos).toHaveLength(4);
    expect(new Set(todos.map((e) => e.idempotencyKey)).size).toBe(1);
    expect(new Set(todos.map((e) => e.secuencia))).toEqual(new Set([1]));
  });

  it('si el servidor ya tenía la respuesta, no la vuelve a enviar', async () => {
    const { simular, cortarYVolver, envios } = preparar(async (metodo, emitir) => {
      if (metodo === 'EnviarRespuesta') throw new Error('Failed to fetch');
      if (metodo === 'ObtenerEstadoSala') {
        emitir('estadoSala', estado({ ultimaSecuencia: 1, preguntasRespondidas: [P1] }));
      }
      return undefined;
    });
    await entrar(simular);
    await userEvent.click(screen.getByLabelText('5'));
    await userEvent.click(screen.getByRole('button', { name: 'Enviar respuesta' }));
    await screen.findByText(/No pudimos enviar tu respuesta/);
    expect(envios()).toHaveLength(3);

    cortarYVolver();

    expect(await screen.findByText('Tu respuesta fue enviada.')).toBeInTheDocument();
    expect(envios()).toHaveLength(3);
  });

  it('si el servidor no encuentra al alumno en una sala, vuelve a pedir el PIN', async () => {
    const { simular, cortarYVolver } = preparar(async (metodo) => {
      if (metodo === 'ObtenerEstadoSala') {
        throw new Error('ALUMNO_NO_EN_SALA: El alumno no se encuentra registrado en ninguna sala.');
      }
      return undefined;
    });
    await entrar(simular);

    cortarYVolver();

    expect(await screen.findByRole('heading', { name: 'Entrar a una sala' })).toBeInTheDocument();
    expect(screen.getByText('No encontramos tu sala. Ingresá el PIN de nuevo.')).toBeInTheDocument();
  });

  it('si falla la red al recuperar, sigue en la sala y ofrece reintentar', async () => {
    let fallar = true;
    const { simular, cortarYVolver, pedidosDeEstado } = preparar(async (metodo, emitir) => {
      if (metodo === 'ObtenerEstadoSala') {
        if (fallar) throw new Error('Failed to fetch');
        emitir('estadoSala', estado({ preguntaActual: pregunta({ preguntaId: P2, indice: 2 }) }));
      }
      return undefined;
    });
    await entrar(simular);

    cortarYVolver();
    expect(await screen.findByText(/No pudimos actualizar tu sala/)).toBeInTheDocument();
    expect(screen.getByText('Pregunta 1 de 2')).toBeInTheDocument();

    fallar = false;
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));

    expect(await screen.findByText('Pregunta 2 de 2')).toBeInTheDocument();
    expect(pedidosDeEstado()).toBe(2);
    expect(screen.queryByText(/No pudimos actualizar tu sala/)).not.toBeInTheDocument();
  });
});
