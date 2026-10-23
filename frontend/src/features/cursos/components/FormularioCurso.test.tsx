import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioCurso } from './FormularioCurso';
import { useCrearCurso } from '../hooks/useCrearCurso';
import { ApiError } from '@/shared/api/cliente';

vi.mock('../hooks/useCrearCurso', () => ({ useCrearCurso: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({
  useAviso: () => ({ mostrar: vi.fn() }),
}));

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  vi.mocked(useCrearCurso).mockReturnValue({
    mutate,
    reset: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
    ...estado,
  } as unknown as ReturnType<typeof useCrearCurso>);
  return mutate;
}

function mostrarFormulario() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <FormularioCurso />
    </QueryClientProvider>,
  );
}

describe('FormularioCurso', () => {
  it('no envía si el nombre y el área están vacíos', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.click(screen.getByRole('button', { name: 'Crear curso' }));

    expect(screen.getByText('Ingresá un nombre')).toBeInTheDocument();
    expect(screen.getByText('Ingresá un área')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('no envía si falta el área', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.type(screen.getByLabelText('Nombre del curso'), 'Álgebra');
    await userEvent.click(screen.getByRole('button', { name: 'Crear curso' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Ingresá un área');
    expect(mutate).not.toHaveBeenCalled();
  });

  it('envía nombre y área cuando son válidos', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.type(screen.getByLabelText('Nombre del curso'), 'Álgebra');
    await userEvent.type(screen.getByLabelText('Área'), 'Matemática');
    await userEvent.click(screen.getByRole('button', { name: 'Crear curso' }));

    expect(mutate).toHaveBeenCalledWith(
      { nombre: 'Álgebra', area: 'Matemática' },
      expect.anything(),
    );
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