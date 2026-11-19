import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useMisExamenes } from './hooks/useMisExamenes';
import type { MiExamen } from './tipos';
import { PaginaMisExamenes } from './PaginaMisExamenes';

vi.mock('./hooks/useMisExamenes', () => ({ useMisExamenes: vi.fn() }));

const aprobado: MiExamen = {
  evaluacionId: 'ev1',
  entregaId: 'en1',
  titulo: 'Parcial 1',
  fechaEntregaUtc: '2026-11-05T14:35:00Z',
  estado: 'Aprobada',
  notaFinal: 8.5,
  puntajeMaximo: 10,
};

const pendiente: MiExamen = {
  evaluacionId: 'ev2',
  entregaId: 'en2',
  titulo: 'Parcial 2',
  fechaEntregaUtc: '2026-11-19T14:10:00Z',
  estado: 'PendienteCorreccion',
};

function simular(estado: Record<string, unknown>) {
  vi.mocked(useMisExamenes).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as ReturnType<typeof useMisExamenes>);
}

const fila = (titulo: string) => screen.getByText(titulo).closest('li') as HTMLElement;

describe('PaginaMisExamenes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el título de la pantalla', () => {
    simular({ data: [] });
    render(<PaginaMisExamenes />);
    expect(screen.getByRole('heading', { level: 1, name: 'Mis exámenes' })).toBeInTheDocument();
  });

  it('muestra un aviso de carga mientras llegan los datos', () => {
    simular({ isPending: true });
    render(<PaginaMisExamenes />);
    expect(screen.getByRole('status')).toHaveTextContent('Cargando tus exámenes…');
  });

  it('con la lista vacía invita a rendir un examen', () => {
    simular({ data: [] });
    render(<PaginaMisExamenes />);
    expect(screen.getByText('Todavía no rendiste ningún examen.')).toBeInTheDocument();
  });

  it('un examen corregido muestra «Corregida» y la nota final', () => {
    simular({ data: [aprobado] });
    render(<PaginaMisExamenes />);
    const celdas = within(fila('Parcial 1'));
    expect(celdas.getByText('Corregida')).toBeInTheDocument();
    expect(celdas.getByText('8,5 / 10')).toBeInTheDocument();
  });

  it('un examen que no es sobre 10 muestra la nota convertida y el puntaje original', () => {
    simular({ data: [{ ...aprobado, notaFinal: 4, puntajeMaximo: 5 }] });
    render(<PaginaMisExamenes />);
    const celdas = within(fila('Parcial 1'));
    expect(celdas.getByText('8 / 10')).toBeInTheDocument();
    expect(celdas.getByText('(4 de 5 puntos obtenidos)')).toBeInTheDocument();
  });

  it('un examen pendiente no muestra ninguna nota', () => {
    simular({ data: [pendiente] });
    render(<PaginaMisExamenes />);
    const celdas = within(fila('Parcial 2'));
    expect(celdas.getByText('Pendiente de corrección')).toBeInTheDocument();
    expect(celdas.queryByText(/\d \/ \d/)).not.toBeInTheDocument();
  });

  it('mantiene el orden que manda el backend (del más nuevo al más viejo)', () => {
    simular({ data: [pendiente, aprobado] });
    render(<PaginaMisExamenes />);
    const filas = screen.getAllByRole('listitem');
    expect(filas[0]).toHaveTextContent('Parcial 2');
    expect(filas[1]).toHaveTextContent('Parcial 1');
  });

  it('con un 403 muestra que no tiene permiso', () => {
    simular({ isError: true, error: new ApiError(403, null) });
    render(<PaginaMisExamenes />);
    expect(screen.getByRole('alert')).toHaveTextContent('No tenés permiso para hacer esto.');
  });

  it('ante un error permite reintentar', async () => {
    const refetch = vi.fn();
    simular({ isError: true, error: new ApiError(500, null), refetch });
    render(<PaginaMisExamenes />);
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(refetch).toHaveBeenCalledOnce();
  });
});
