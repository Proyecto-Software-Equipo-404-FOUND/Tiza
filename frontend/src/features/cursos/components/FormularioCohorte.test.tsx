import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioCohorte } from './FormularioCohorte';
import { useCrearCohorte } from '../hooks/useCrearCohorte';
import { ApiError } from '@/shared/api/cliente';

vi.mock('../hooks/useCrearCohorte', () => ({ useCrearCohorte: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({
  useAviso: () => ({ mostrar: vi.fn() }),
}));

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  vi.mocked(useCrearCohorte).mockReturnValue({
    mutate,
    reset: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
    ...estado,
  } as unknown as ReturnType<typeof useCrearCohorte>);
  return mutate;
}

function mostrarFormulario() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <FormularioCohorte cursoId='c-1' />
    </QueryClientProvider>,
  );
}

describe('FormularioCohorte', () => {
  it('no envía si el nombre está vacío', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.click(screen.getByRole('button', { name: 'Crear cohorte' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Ingresá un nombre');
    expect(mutate).not.toHaveBeenCalled();
  });

  it('envía el nombre cuando es válido', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.type(screen.getByLabelText('Nombre de la cohorte'), 'Cohorte 2026 - Noche');
    await userEvent.click(screen.getByRole('button', { name: 'Crear cohorte' }));

    expect(mutate).toHaveBeenCalledWith({ nombre: 'Cohorte 2026 - Noche' }, expect.anything());
  });

  it('deshabilita el botón mientras envía', () => {
    simular({ isPending: true });
    mostrarFormulario();

    expect(screen.getByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });

  it('ante un 409 muestra el conflicto y el botón para recargar', () => {
    simular({ isError: true, error: new ApiError(409, null) });
    mostrarFormulario();

    expect(screen.getByRole('alert')).toHaveTextContent('Alguien modificó este dato');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});