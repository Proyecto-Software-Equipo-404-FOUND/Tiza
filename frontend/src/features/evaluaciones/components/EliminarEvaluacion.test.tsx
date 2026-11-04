import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useEliminarEvaluacion } from '../hooks/useEliminarEvaluacion';
import { EliminarEvaluacion } from './EliminarEvaluacion';

const mostrarAviso = vi.fn();
vi.mock('../hooks/useEliminarEvaluacion', () => ({ useEliminarEvaluacion: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: () => ({ mostrar: mostrarAviso }) }));

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  const reset = vi.fn();
  vi.mocked(useEliminarEvaluacion).mockReturnValue({
    mutate,
    reset,
    isPending: false,
    error: null,
    ...estado,
  } as unknown as ReturnType<typeof useEliminarEvaluacion>);
  return { mutate, reset };
}

const mostrar = () =>
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={['/evaluaciones/e1']}>
        <Routes>
          <Route path='/evaluaciones/:id' element={<EliminarEvaluacion evaluacionId='e1' />} />
          <Route path='/evaluaciones' element={<p>LISTA</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );

describe('EliminarEvaluacion', () => {
  it('pide confirmación antes de borrar y se puede cancelar', async () => {
    const { mutate } = simular();
    mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar borrador' }));
    expect(screen.getByRole('dialog', { name: '¿Eliminar este borrador?' })).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Eliminar borrador' })).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('al confirmar borra, avisa y vuelve a la lista', async () => {
    const { mutate } = simular();
    mutate.mockImplementation((_vacio, opciones) => opciones.onSuccess());
    mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar borrador' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));

    expect(mutate).toHaveBeenCalledTimes(1);
    expect(mostrarAviso).toHaveBeenCalledWith('exito', 'Borrador eliminado.');
    expect(await screen.findByText('LISTA')).toBeInTheDocument();
  });

  it('ante un 409 explica que solo se borran borradores y ofrece «Recargar»', async () => {
    const { reset } = simular({
      error: new ApiError(409, { status: 409, detail: 'Solo se pueden borrar evaluaciones en estado Borrador.' }),
    });
    mostrar();
    expect(screen.getByText('Solo se pueden borrar evaluaciones en estado Borrador.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Recargar' }));
    expect(reset).toHaveBeenCalledTimes(1);
  });

  it('ante un 404 muestra el mensaje y no ofrece recargar', () => {
    simular({ error: new ApiError(404, { status: 404 }) });
    mostrar();
    expect(screen.getByText('No se encontró lo que buscás.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Recargar' })).not.toBeInTheDocument();
  });
});
