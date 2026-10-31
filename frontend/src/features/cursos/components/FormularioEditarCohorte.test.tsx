import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioEditarCohorte } from './FormularioEditarCohorte';
import { useActualizarCohorte } from '../hooks/useActualizarCohorte';
import { DOCENTE_A_ID } from '../semillas';
import { ApiError } from '@/shared/api/cliente';
import type { Cohorte } from '@/shared/api/tipos';
import { COHORTE_1_ID, CURSO_1_ID } from '@/mocks/datos';

vi.mock('../hooks/useActualizarCohorte', () => ({ useActualizarCohorte: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({
  useAviso: () => ({ mostrar: vi.fn() }),
}));

const cohorte: Cohorte = {
  id: COHORTE_1_ID,
  cursoId: CURSO_1_ID,
  docenteId: DOCENTE_A_ID,
  nombre: 'Cohorte 2026 - Mañana',
  periodo: '2026-1C',
  activo: true,
  fechaCreacionUtc: '2026-10-09T12:00:00Z',
};

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  vi.mocked(useActualizarCohorte).mockReturnValue({
    mutate,
    reset: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
    ...estado,
  } as unknown as ReturnType<typeof useActualizarCohorte>);
  return mutate;
}

function mostrarFormulario(datos: Cohorte = cohorte, onTerminar = vi.fn()) {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <FormularioEditarCohorte cursoId={CURSO_1_ID} cohorte={datos} onTerminar={onTerminar} />
    </QueryClientProvider>,
  );
  return onTerminar;
}

describe('FormularioEditarCohorte', () => {
  it('empieza con los datos actuales de la cohorte', () => {
    simular();
    mostrarFormulario();
    expect(screen.getByLabelText('Nombre de la cohorte')).toHaveValue('Cohorte 2026 - Mañana');
    expect(screen.getByLabelText('Período')).toHaveValue('2026-1C');
    expect(screen.getByLabelText('Docente')).toHaveValue(DOCENTE_A_ID);
  });

  it('conserva al docente actual aunque no esté en la lista provisoria', () => {
    simular();
    mostrarFormulario({ ...cohorte, docenteId: 'otro-docente' });
    expect(screen.getByRole('option', { name: 'Docente actual' })).toBeInTheDocument();
    expect(screen.getByLabelText('Docente')).toHaveValue('otro-docente');
  });

  it('valida el largo máximo del período', async () => {
    const mutate = simular();
    mostrarFormulario();
    const periodo = screen.getByLabelText('Período');
    await userEvent.clear(periodo);
    await userEvent.type(periodo, 'x'.repeat(31));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(screen.getByText('Máximo 30 caracteres')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('envía los datos corregidos', async () => {
    const mutate = simular();
    mostrarFormulario();
    const nombre = screen.getByLabelText('Nombre de la cohorte');
    await userEvent.clear(nombre);
    await userEvent.type(nombre, 'Cohorte 2026 - Noche');
    await userEvent.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(mutate).toHaveBeenCalledWith(
      { nombre: 'Cohorte 2026 - Noche', periodo: '2026-1C', docenteId: DOCENTE_A_ID },
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

  it('ante un 409 muestra el mensaje y ofrece recargar', () => {
    simular({ isError: true, error: new ApiError(409, { status: 409, title: 'Conflicto' }) });
    mostrarFormulario();
    expect(screen.getByRole('alert')).toHaveTextContent('Alguien modificó este dato');
    expect(screen.getByRole('button', { name: 'Recargar' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
  });

  it('«Recargar» sale de la edición para ver los datos actualizados', async () => {
    simular({ isError: true, error: new ApiError(409, { status: 409, title: 'Conflicto' }) });
    const onTerminar = mostrarFormulario();
    await userEvent.click(screen.getByRole('button', { name: 'Recargar' }));
    expect(onTerminar).toHaveBeenCalledTimes(1);
  });
});
