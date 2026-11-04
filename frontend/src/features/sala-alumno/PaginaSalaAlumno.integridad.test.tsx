import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { PaginaSalaAlumno } from './PaginaSalaAlumno';
import type { PreguntaActual } from './tipos';

const SALA = '11111111-1111-4111-8111-111111111111';
const P1 = '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60';
const enSegundos = (s: number) => new Date(Date.now() + s * 1000).toISOString();

const pregunta: PreguntaActual = {
  salaId: SALA,
  preguntaId: P1,
  indice: 1,
  totalPreguntas: 1,
  tipo: 'Abierta',
  enunciado: 'Explicá qué es una variable',
  opciones: null,
  puntajeMaximo: 2,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(300),
};

function cambiarVisibilidad(estado: 'hidden' | 'visible') {
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: estado });
  document.dispatchEvent(new Event('visibilitychange'));
}

async function empezarExamen() {
  const { conexion, simular } = crearConexionFalsa();
  const invocar = vi.fn(async (..._args: unknown[]) => undefined as unknown);
  conexion.invocar = invocar as typeof conexion.invocar;
  render(<PaginaSalaAlumno conexion={conexion} esperar={async () => {}} />);

  await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar' })).toBeEnabled());
  await userEvent.type(screen.getByLabelText('PIN'), '123456');
  await userEvent.click(screen.getByLabelText('Entendí el aviso y acepto'));
  await userEvent.click(screen.getByRole('button', { name: 'Entrar' }));
  await screen.findByRole('heading', { name: 'Estás en la sala' });
  return { invocar, simular };
}

describe('PaginaSalaAlumno · integridad (CU-04)', () => {
  afterEach(() => cambiarVisibilidad('visible'));

  it('durante la espera no se registra la salida de la pantalla', async () => {
    const { invocar } = await empezarExamen();
    invocar.mockClear();
    cambiarVisibilidad('hidden');
    cambiarVisibilidad('visible');
    expect(invocar).not.toHaveBeenCalled();
  });

  it('con el examen en curso registra la salida y recuerda que se está registrando', async () => {
    const { invocar, simular } = await empezarExamen();
    act(() => {
      simular('salaIniciada', { salaId: SALA, iniciadaUtc: new Date().toISOString(), totalPreguntas: 1, fechaLimiteUtc: enSegundos(300) });
      simular('preguntaActual', pregunta);
    });
    expect(screen.getByText('Se registra si salís de esta pantalla.')).toBeInTheDocument();
    invocar.mockClear();

    act(() => cambiarVisibilidad('hidden'));
    act(() => cambiarVisibilidad('visible'));

    await waitFor(() =>
      expect(invocar).toHaveBeenCalledWith(
        'RegistrarEventoFoco',
        expect.objectContaining({ tipoEvento: 'visibilitychange', preguntaId: P1 }),
      ),
    );
  });

  it('al recibir salaFinalizada muestra «Examen entregado»', async () => {
    const { simular } = await empezarExamen();
    act(() => simular('salaFinalizada', { salaId: SALA, finalizadaUtc: new Date().toISOString(), motivo: 'Docente' }));
    expect(screen.getByRole('heading', { name: 'Examen entregado' })).toBeInTheDocument();
  });
});
