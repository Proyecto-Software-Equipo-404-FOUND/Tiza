import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { PaginaSalaAlumno } from './PaginaSalaAlumno';

const SALA = '11111111-1111-4111-8111-111111111111';

function preparar(invocar: (metodo: string, ...args: unknown[]) => Promise<void> = async () => {}) {
  const { conexion, simular } = crearConexionFalsa();
  conexion.invocar = vi.fn(invocar);
  render(<PaginaSalaAlumno conexion={conexion} />);
  return { conexion, simular };
}

// El botón queda deshabilitado hasta que la conexión pasa a «conectado»
const botonEntrar = () => screen.getByRole('button', { name: 'Entrar' });
async function escribirYEntrar(pin: string) {
  await waitFor(() => expect(botonEntrar()).toBeEnabled());
  await userEvent.type(screen.getByLabelText('PIN'), pin);
  await userEvent.click(botonEntrar());
}

describe('PaginaSalaAlumno', () => {
  it('muestra el formulario del PIN y el estado de la conexión', async () => {
    preparar();
    expect(screen.getByRole('heading', { name: 'Entrar a una sala' })).toBeInTheDocument();
    expect(await screen.findByText('Conectado')).toBeInTheDocument();
  });

  it('solo acepta dígitos al escribir', async () => {
    preparar();
    await waitFor(() => expect(botonEntrar()).toBeEnabled());
    await userEvent.type(screen.getByLabelText('PIN'), '12ab34');
    expect(screen.getByLabelText('PIN')).toHaveValue('1234');
  });

  it('no invoca al hub si el PIN no tiene 6 dígitos', async () => {
    const { conexion } = preparar();
    await escribirYEntrar('123');

    expect(screen.getByText('El PIN tiene 6 dígitos')).toBeInTheDocument();
    expect(conexion.invocar).not.toHaveBeenCalled();
  });

  it('une al alumno con el PIN y muestra la sala de espera', async () => {
    const { conexion } = preparar();
    await escribirYEntrar('123456');

    expect(await screen.findByRole('heading', { name: 'Estás en la sala' })).toBeInTheDocument();
    expect(conexion.invocar).toHaveBeenCalledWith('UnirseASala', 123456);
  });

  it('muestra un mensaje claro cuando la sala está llena y permite reintentar', async () => {
    preparar(async () => {
      throw new Error('SALA_LLENA: la sala alcanzó su capacidad máxima.');
    });
    await escribirYEntrar('123456');

    expect(await screen.findByText('La sala está llena. Avisale a tu docente.')).toBeInTheDocument();
    expect(screen.getByLabelText('PIN')).toBeEnabled();
  });

  it('entiende el mensaje actual del hub cuando la sala no existe', async () => {
    preparar(async () => {
      throw new Error('La sala no existe, está inactiva o venció.');
    });
    await escribirYEntrar('123456');

    expect(await screen.findByText(/No encontramos una sala con ese PIN/)).toBeInTheDocument();
  });

  it('trata «ya registrado» como un ingreso exitoso (reconexión)', async () => {
    const { conexion } = preparar(async (metodo) => {
      if (metodo === 'UnirseASala') throw new Error('El alumno ya está registrado en la sala.');
    });
    await escribirYEntrar('123456');

    expect(await screen.findByRole('heading', { name: 'Estás en la sala' })).toBeInTheDocument();
    expect(conexion.invocar).toHaveBeenCalledWith('ObtenerEstadoSala');
  });

  it('pasa a «la evaluación empezó» cuando llega salaIniciada', async () => {
    const { simular } = preparar();
    await escribirYEntrar('123456');
    await screen.findByRole('heading', { name: 'Estás en la sala' });

    act(() =>
      simular('salaIniciada', {
        salaId: SALA,
        iniciadaUtc: '2026-10-28T14:00:00.000Z',
        totalPreguntas: 2,
        fechaLimiteUtc: '2026-10-28T14:30:00.000Z',
      }),
    );

    expect(screen.getByRole('heading', { name: 'La evaluación empezó' })).toBeInTheDocument();
  });

  it('si la sala ya estaba en curso al entrar, va directo a la evaluación', async () => {
    const { simular } = preparar(async (metodo) => {
      if (metodo === 'UnirseASala') simular('estadoSala', { salaId: SALA, estado: 'EnCurso', fechaLimiteUtc: null });
    });
    await escribirYEntrar('444444');

    expect(await screen.findByRole('heading', { name: 'La evaluación empezó' })).toBeInTheDocument();
  });

  it('muestra el cierre cuando llega salaFinalizada por tiempo agotado', async () => {
    const { simular } = preparar();
    await escribirYEntrar('123456');
    await screen.findByRole('heading', { name: 'Estás en la sala' });

    act(() =>
      simular('salaFinalizada', {
        salaId: SALA,
        finalizadaUtc: '2026-10-28T14:30:00.000Z',
        motivo: 'TiempoAgotado',
      }),
    );

    expect(screen.getByRole('heading', { name: 'La sala finalizó' })).toBeInTheDocument();
    expect(screen.getByText('Se terminó el tiempo.')).toBeInTheDocument();
  });
});
