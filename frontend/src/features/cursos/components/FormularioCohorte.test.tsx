import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioCohorte } from './FormularioCohorte';
import { useCrearCohorte } from '../hooks/useCrearCohorte';
import { DOCENTE_A_ID } from '../semillas';
import { ApiError } from '@/shared/api/cliente';
import { CURSO_1_ID } from '@/mocks/datos';

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
      <FormularioCohorte cursoId={CURSO_1_ID} />
    </QueryClientProvider>,
  );
}

describe('FormularioCohorte', () => {
  it('no envía si el nombre y el período están vacíos', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.click(screen.getByRole('button', { name: 'Crear cohorte' }));

    expect(screen.getByText('Ingresá un nombre')).toBeInTheDocument();
    expect(screen.getByText('Ingresá un período')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('no envía si el período supera los 30 caracteres', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.type(screen.getByLabelText('Nombre de la cohorte'), 'Cohorte Noche');
    await userEvent.type(screen.getByLabelText('Período'), 'x'.repeat(31));
    await userEvent.click(screen.getByRole('button', { name: 'Crear cohorte' }));

    expect(screen.getByText('Máximo 30 caracteres')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('envía nombre, período y docente cuando es válido', async () => {
    const mutate = simular();
    mostrarFormulario();

    await userEvent.type(screen.getByLabelText('Nombre de la cohorte'), 'Cohorte 2026 - Noche');
    await userEvent.type(screen.getByLabelText('Período'), '2026-1C');
    await userEvent.click(screen.getByRole('button', { name: 'Crear cohorte' }));

    expect(mutate).toHaveBeenCalledWith(
      { nombre: 'Cohorte 2026 - Noche', periodo: '2026-1C', docenteId: DOCENTE_A_ID },
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