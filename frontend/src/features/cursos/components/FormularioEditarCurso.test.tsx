import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioEditarCurso } from './FormularioEditarCurso';
import { useActualizarCurso } from '../hooks/useActualizarCurso';
import { ApiError } from '@/shared/api/cliente';
import type { Curso } from '@/shared/api/tipos';
import { CURSO_1_ID, INSTITUCION_A } from '@/mocks/datos';

vi.mock('../hooks/useActualizarCurso', () => ({ useActualizarCurso: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({
  useAviso: () => ({ mostrar: vi.fn() }),
}));

const curso: Curso = {
  id: CURSO_1_ID,
  institucionId: INSTITUCION_A,
  nombre: 'Programación I',
  area: 'Sistemas',
  activo: true,
  fechaCreacionUtc: '2026-10-09T12:00:00Z',
};

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  vi.mocked(useActualizarCurso).mockReturnValue({
    mutate,
    reset: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
    ...estado,
  } as unknown as ReturnType<typeof useActualizarCurso>);
  return mutate;
}

function mostrarFormulario(onTerminar = vi.fn()) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <FormularioEditarCurso curso={curso} onTerminar={onTerminar} />
    </QueryClientProvider>,
  );
  return onTerminar;
}

describe('FormularioEditarCurso', () => {
  it('empieza con los datos actuales del curso', () => {
    simular();
    mostrarFormulario();
    expect(screen.getByLabelText('Nombre del curso')).toHaveValue('Programación I');
    expect(screen.getByLabelText('Área')).toHaveValue('Sistemas');
  });

  it('no envía si el nombre queda vacío', async () => {
    const mutate = simular();
    mostrarFormulario();
    await userEvent.clear(screen.getByLabelText('Nombre del curso'));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(screen.getByText('Ingresá un nombre')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('envía los datos corregidos y sin espacios sobrantes', async () => {
    const mutate = simular();
    mostrarFormulario();
    const nombre = screen.getByLabelText('Nombre del curso');
    await userEvent.clear(nombre);
    await userEvent.type(nombre, '  Programación II  ');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(mutate).toHaveBeenCalledWith(
      { nombre: 'Programación II', area: 'Sistemas' },
      expect.any(Object),
    );
  });

  it('cancelar avisa a la pantalla sin enviar nada', async () => {
    const mutate = simular();
    const onTerminar = mostrarFormulario();
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));

    expect(onTerminar).toHaveBeenCalledTimes(1);
    expect(mutate).not.toHaveBeenCalled();
  });

  it('deshabilita el botón mientras guarda', () => {
    simular({ isPending: true });
    mostrarFormulario();
    expect(screen.getByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });

  it('ante un 409 muestra el mensaje y ofrece recargar', () => {
    simular({ isError: true, error: new ApiError(409, { status: 409, title: 'Conflicto' }) });
    mostrarFormulario();
    expect(screen.getByRole('alert')).toHaveTextContent('Alguien modificó este dato');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});
