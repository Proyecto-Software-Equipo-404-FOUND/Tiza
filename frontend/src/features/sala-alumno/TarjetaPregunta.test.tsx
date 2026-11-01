import { render, screen } from '@testing-library/react';
import type { ComponentProps } from 'react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TarjetaPregunta } from './TarjetaPregunta';
import type { PreguntaActual } from './tipos';

const base: PreguntaActual = {
  salaId: '11111111-1111-4111-8111-111111111111',
  preguntaId: '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60',
  indice: 1,
  totalPreguntas: 2,
  tipo: 'OpcionMultiple',
  enunciado: '¿Cuánto es 2 + 3?',
  opciones: [
    { id: 'op-5', texto: '5' },
    { id: 'op-23', texto: '23' },
  ],
  puntajeMaximo: 2,
  fechaInicioUtc: '2026-10-28T14:00:00.000Z',
  fechaLimiteUtc: '2026-10-28T14:10:00.000Z',
};

function mostrar(cambios: Partial<ComponentProps<typeof TarjetaPregunta>> = {}) {
  const onEnviar = vi.fn();
  render(
    <TarjetaPregunta pregunta={base} respondida={false} enviando={false} error={null} desfaseMs={0} onEnviar={onEnviar} {...cambios} />,
  );
  return onEnviar;
}

describe('TarjetaPregunta', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    vi.setSystemTime(new Date('2026-10-28T14:00:00.000Z'));
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('muestra el enunciado, el avance y el puntaje', () => {
    mostrar();
    expect(screen.getByText('Pregunta 1 de 2')).toBeInTheDocument();
    expect(screen.getByText('2 puntos')).toBeInTheDocument();
    expect(screen.getByText('¿Cuánto es 2 + 3?')).toBeInTheDocument();
  });

  it('el botón está deshabilitado hasta elegir una opción y envía el id de la opción', async () => {
    const usuario = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const onEnviar = mostrar();
    expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeDisabled();

    await usuario.click(screen.getByLabelText('5'));
    await usuario.click(screen.getByRole('button', { name: 'Enviar respuesta' }));
    expect(onEnviar).toHaveBeenCalledWith('op-5');
  });

  it('en una pregunta abierta muestra el cuadro de texto con contador', async () => {
    const usuario = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const onEnviar = mostrar({ pregunta: { ...base, tipo: 'Abierta', opciones: null } });

    await usuario.type(screen.getByLabelText('Tu respuesta'), 'Un nombre');
    expect(screen.getByText('9 / 10000')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Enviar respuesta' }));
    expect(onEnviar).toHaveBeenCalledWith('Un nombre');
  });

  it('no deja enviar una respuesta de solo espacios', async () => {
    const usuario = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    mostrar({ pregunta: { ...base, tipo: 'Abierta', opciones: null } });
    await usuario.type(screen.getByLabelText('Tu respuesta'), '   ');
    expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeDisabled();
  });

  it('muestra «respuesta enviada» cuando ya respondió', () => {
    mostrar({ respondida: true });
    expect(screen.getByText('Tu respuesta fue enviada.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Enviar respuesta' })).not.toBeInTheDocument();
  });

  it('en la última pregunta avisa que espere el cierre', () => {
    mostrar({ respondida: true, pregunta: { ...base, indice: 2 } });
    expect(screen.getByText(/última pregunta/)).toBeInTheDocument();
  });

  it('con el tiempo agotado bloquea el envío y lo avisa', () => {
    mostrar({ pregunta: { ...base, fechaLimiteUtc: '2026-10-28T13:59:00.000Z' } });
    expect(screen.getByText('Se terminó el tiempo para responder.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeDisabled();
  });

  it('muestra el error de envío recibido', () => {
    mostrar({ error: 'No pudimos enviar tu respuesta.' });
    expect(screen.getByText('No pudimos enviar tu respuesta.')).toBeInTheDocument();
  });
});
