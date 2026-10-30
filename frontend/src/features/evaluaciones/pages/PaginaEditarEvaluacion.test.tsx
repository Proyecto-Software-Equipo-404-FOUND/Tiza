import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useActualizarEvaluacion } from '../hooks/useActualizarEvaluacion';
import { useEvaluacion } from '../hooks/useEvaluacion';
import type { Evaluacion } from '../tipos';
import { PaginaEditarEvaluacion } from './PaginaEditarEvaluacion';

const mostrarAviso = vi.fn();
vi.mock('../hooks/useEvaluacion', () => ({ useEvaluacion: vi.fn() }));
vi.mock('../hooks/useActualizarEvaluacion', () => ({ useActualizarEvaluacion: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: () => ({ mostrar: mostrarAviso }) }));

const evaluacion: Evaluacion = {
  id: 'e1',
  institucionId: 'i1',
  titulo: 'TP Funciones',
  tipo: 'Cuestionario',
  estado: 'Borrador',
  cohorteId: null,
  preguntasJson: {
    version: 1,
    preguntas: [{ id: 'p1', enunciado: 'Escribí una función', tipo: 'Abierta', puntajeMaximo: 10 }],
  },
  fechaCreacion: '2026-10-24T10:00:00Z',
  fechaActualizacion: '2026-10-24T10:00:00Z',
  version: 1,
};

function simular(lectura: Record<string, unknown> = {}, escritura: Record<string, unknown> = {}) {
  vi.mocked(useEvaluacion).mockReturnValue({
    data: evaluacion,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...lectura,
  } as unknown as ReturnType<typeof useEvaluacion>);
  const mutate = vi.fn();
  const reset = vi.fn();
  vi.mocked(useActualizarEvaluacion).mockReturnValue({
    mutate,
    reset,
    isPending: false,
    error: null,
    ...escritura,
  } as unknown as ReturnType<typeof useActualizarEvaluacion>);
  return { mutate, reset };
}

const renderizar = () =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={['/evaluaciones/e1/editar']}>
        <Routes>
          <Route path='/evaluaciones/:evaluacionId/editar' element={<PaginaEditarEvaluacion />} />
          <Route path='/evaluaciones/:evaluacionId' element={<p>DETALLE</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );

describe('PaginaEditarEvaluacion', () => {
  it('pide la evaluación del id de la URL y muestra el spinner', () => {
    simular({ data: undefined, isPending: true });
    renderizar();
    expect(useEvaluacion).toHaveBeenCalledWith('e1');
    expect(screen.getByRole('status')).toHaveTextContent('Cargando la evaluación');
  });

  it('muestra el error de carga con un botón para reintentar', () => {
    simular({ data: undefined, isError: true, error: new ApiError(404, { status: 404 }) });
    renderizar();
    expect(screen.getByText('No se encontró lo que buscás.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('una evaluación publicada no se puede editar', () => {
    simular({ data: { ...evaluacion, estado: 'Publicada' } });
    renderizar();
    expect(screen.getByText('Esta evaluación ya no se puede editar')).toBeInTheDocument();
    expect(screen.queryByLabelText('Título')).not.toBeInTheDocument();
  });

  it('carga los datos del borrador en el formulario', () => {
    simular();
    renderizar();
    expect(screen.getByLabelText('Título')).toHaveValue('TP Funciones');
    expect(screen.getByLabelText('Enunciado de la pregunta 1')).toHaveValue('Escribí una función');
  });

  it('guarda los cambios, avisa y vuelve al detalle', async () => {
    const { mutate } = simular();
    mutate.mockImplementation((_datos, opciones) => opciones.onSuccess());
    renderizar();

    const titulo = screen.getByLabelText('Título');
    await userEvent.clear(titulo);
    await userEvent.type(titulo, 'TP Funciones II');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(mutate).toHaveBeenCalledWith(
      expect.objectContaining({ titulo: 'TP Funciones II', tipo: 'Cuestionario' }),
      expect.any(Object),
    );
    expect(mostrarAviso).toHaveBeenCalledWith('exito', 'Cambios guardados.');
    expect(await screen.findByText('DETALLE')).toBeInTheDocument();
  });

  it('ante un 409 «Recargar» vuelve a pedir los datos y sale de la edición', async () => {
    const { reset } = simular({}, { error: new ApiError(409, { status: 409, title: 'Conflicto' }) });
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Recargar' }));
    expect(reset).toHaveBeenCalledTimes(1);
    expect(await screen.findByText('DETALLE')).toBeInTheDocument();
  });
});
