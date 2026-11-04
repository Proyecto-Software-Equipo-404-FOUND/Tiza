import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useCrearEvaluacion } from '../hooks/useCrearEvaluacion';
import { PaginaNuevaEvaluacion } from './PaginaNuevaEvaluacion';

const mostrarAviso = vi.fn();
vi.mock('../hooks/useCrearEvaluacion', () => ({ useCrearEvaluacion: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: () => ({ mostrar: mostrarAviso }) }));

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  vi.mocked(useCrearEvaluacion).mockReturnValue({
    mutate,
    isPending: false,
    error: null,
    ...estado,
  } as unknown as ReturnType<typeof useCrearEvaluacion>);
  return mutate;
}

const renderizar = () =>
  render(
    <MemoryRouter initialEntries={['/evaluaciones/nueva']}>
      <Routes>
        <Route path='/evaluaciones/nueva' element={<PaginaNuevaEvaluacion />} />
        <Route path='/evaluaciones' element={<p>LISTA</p>} />
        <Route path='/evaluaciones/:evaluacionId' element={<p>DETALLE</p>} />
      </Routes>
    </MemoryRouter>,
  );

describe('PaginaNuevaEvaluacion', () => {
  it('muestra el título de la página y el formulario vacío', () => {
    simular();
    renderizar();
    expect(screen.getByRole('heading', { level: 1, name: 'Nueva evaluación' })).toBeInTheDocument();
    expect(screen.getByLabelText('Título')).toHaveValue('');
  });

  it('guarda el borrador y lleva al detalle con un aviso', async () => {
    const mutate = simular();
    mutate.mockImplementation((_datos, opciones) => opciones.onSuccess({ id: 'nueva-1' }));
    renderizar();

    await userEvent.type(screen.getByLabelText('Título'), 'Parcial');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar borrador' }));

    expect(mutate).toHaveBeenCalledWith(
      { titulo: 'Parcial', tipo: 'Examen', preguntasJson: { version: 1, preguntas: [] } },
      expect.any(Object),
    );
    expect(mostrarAviso).toHaveBeenCalledWith('exito', 'Borrador creado.');
    expect(await screen.findByText('DETALLE')).toBeInTheDocument();
  });

  it('cancelar vuelve a la lista', async () => {
    simular();
    renderizar();
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(await screen.findByText('LISTA')).toBeInTheDocument();
  });

  it('muestra el error del servidor', () => {
    simular({ error: new ApiError(400, { status: 400, detail: 'Faltan datos.' }) });
    renderizar();
    expect(screen.getByText('Faltan datos.')).toBeInTheDocument();
  });
});
