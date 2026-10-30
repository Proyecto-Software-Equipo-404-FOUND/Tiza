import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import { useCohortes } from '@/features/cursos/hooks/useCohortes';
import { useCursos } from '@/features/cursos/hooks/useCursos';
import { ApiError } from '@/shared/api/cliente';
import { usePublicarEvaluacion } from '../hooks/usePublicarEvaluacion';
import type { Evaluacion } from '../tipos';
import { PublicarEvaluacion } from './PublicarEvaluacion';

const mostrarAviso = vi.fn();
vi.mock('@/features/cursos/hooks/useCursos', () => ({ useCursos: vi.fn() }));
vi.mock('@/features/cursos/hooks/useCohortes', () => ({ useCohortes: vi.fn() }));
vi.mock('../hooks/usePublicarEvaluacion', () => ({ usePublicarEvaluacion: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: () => ({ mostrar: mostrarAviso }) }));

const evaluacion: Evaluacion = {
  id: 'e1',
  institucionId: 'i1',
  titulo: 'Parcial',
  tipo: 'Examen',
  estado: 'Borrador',
  cohorteId: null,
  preguntasJson: {
    version: 1,
    preguntas: [{ id: 'p1', enunciado: '¿Algo?', tipo: 'Abierta', puntajeMaximo: 10 }],
  },
  fechaCreacion: '2026-10-24T10:00:00Z',
  fechaActualizacion: '2026-10-24T10:00:00Z',
  version: 4,
};

function simular(escritura: Record<string, unknown> = {}) {
  vi.mocked(useCursos).mockReturnValue({
    isPending: false,
    isError: false,
    data: { items: [{ id: 'c1', nombre: 'Programación I' }] },
  } as unknown as ReturnType<typeof useCursos>);
  vi.mocked(useCohortes).mockReturnValue({
    isPending: false,
    isError: false,
    data: [{ id: 'h1', nombre: 'Cohorte Noche', periodo: '2026-2C' }],
  } as unknown as ReturnType<typeof useCohortes>);
  const mutate = vi.fn();
  const reset = vi.fn();
  vi.mocked(usePublicarEvaluacion).mockReturnValue({
    mutate,
    reset,
    isPending: false,
    error: null,
    ...escritura,
  } as unknown as ReturnType<typeof usePublicarEvaluacion>);
  return { mutate, reset };
}

const mostrar = (datos: Evaluacion = evaluacion) =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <PublicarEvaluacion evaluacion={datos} />
    </QueryClientProvider>,
  );

async function elegirCohorte() {
  await userEvent.selectOptions(screen.getByLabelText('Curso'), 'c1');
  await userEvent.selectOptions(screen.getByLabelText('Cohorte'), 'h1');
}

describe('PublicarEvaluacion', () => {
  it('no deja publicar hasta elegir curso y cohorte', async () => {
    simular();
    mostrar();
    expect(screen.queryByLabelText('Cohorte')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publicar' })).toBeDisabled();

    await userEvent.selectOptions(screen.getByLabelText('Curso'), 'c1');
    expect(useCohortes).toHaveBeenLastCalledWith('c1');
    expect(screen.getByRole('button', { name: 'Publicar' })).toBeDisabled();

    await userEvent.selectOptions(screen.getByLabelText('Cohorte'), 'h1');
    expect(screen.getByRole('button', { name: 'Publicar' })).toBeEnabled();
  });

  it('publica enviando la versión que se está viendo y la cohorte elegida', async () => {
    const { mutate } = simular();
    mutate.mockImplementation((_datos, opciones) => opciones.onSuccess());
    mostrar();
    await elegirCohorte();
    await userEvent.click(screen.getByRole('button', { name: 'Publicar' }));

    expect(mutate).toHaveBeenCalledWith({ versionEsperada: 4, cohorteId: 'h1' }, expect.any(Object));
    expect(mostrarAviso).toHaveBeenCalledWith('exito', 'Evaluación publicada.');
  });

  it('sin preguntas no se puede publicar y lo explica', async () => {
    simular();
    mostrar({ ...evaluacion, preguntasJson: { version: 1, preguntas: [] } });
    await elegirCohorte();
    expect(screen.getByText('Agregá al menos una pregunta para poder publicar.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Publicar' })).toBeDisabled();
  });

  it('avisa si el curso no tiene cohortes', async () => {
    simular();
    vi.mocked(useCohortes).mockReturnValue({ isPending: false, isError: false, data: [] } as unknown as ReturnType<
      typeof useCohortes
    >);
    mostrar();
    await userEvent.selectOptions(screen.getByLabelText('Curso'), 'c1');
    expect(screen.getByText('Este curso no tiene cohortes')).toBeInTheDocument();
  });

  it('ante un 409 muestra el motivo y «Recargar» pide los datos de nuevo', async () => {
    const { reset } = simular({
      error: new ApiError(409, { status: 409, detail: 'La evaluación cambió. Recargala e intentá de nuevo.' }),
    });
    mostrar();
    expect(screen.getByText('La evaluación cambió. Recargala e intentá de nuevo.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Recargar' }));
    expect(reset).toHaveBeenCalledTimes(1);
  });

  it('muestra el detalle de un 400 sin ofrecer recargar', () => {
    simular({ error: new ApiError(400, { status: 400, detail: 'La evaluación no tiene preguntas.' }) });
    mostrar();
    expect(screen.getByText('La evaluación no tiene preguntas.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Recargar' })).not.toBeInTheDocument();
  });

  it('mientras publica deshabilita el botón', () => {
    simular({ isPending: true });
    mostrar();
    expect(screen.getByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });
});
