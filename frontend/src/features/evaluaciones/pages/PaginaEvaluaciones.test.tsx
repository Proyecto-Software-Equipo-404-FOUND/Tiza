import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaEvaluaciones } from './PaginaEvaluaciones';
import { useEvaluaciones } from '../hooks/useEvaluaciones';
import { ApiError } from '@/shared/api/cliente';
import type { Evaluacion } from '../tipos';

vi.mock('../hooks/useEvaluaciones', () => ({ useEvaluaciones: vi.fn() }));

type Estado = ReturnType<typeof useEvaluaciones>;

function simular(estado: Partial<Estado>) {
  vi.mocked(useEvaluaciones).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as Estado);
}

const evaluacion = (datos: Partial<Evaluacion>): Evaluacion => ({
  id: 'e1',
  institucionId: 'i1',
  titulo: 'Parcial 1',
  tipo: 'Examen',
  estado: 'Publicada',
  cohorteId: 'c1',
  preguntasJson: {
    version: 1,
    preguntas: [{ id: 'p1', enunciado: '¿Algo?', tipo: 'Abierta', puntajeMaximo: 10 }],
  },
  fechaCreacion: '2026-10-20T12:00:00Z',
  fechaActualizacion: '2026-10-22T12:00:00Z',
  version: 2,
  ...datos,
});

// La página tiene enlaces al detalle, que necesitan un router
const renderizar = () =>
  render(
    <MemoryRouter>
      <PaginaEvaluaciones />
    </MemoryRouter>,
  );

describe('PaginaEvaluaciones', () => {
  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    renderizar();
    expect(screen.getByRole('status')).toHaveTextContent('Cargando evaluaciones');
  });

  it('muestra el error con un botón para reintentar', () => {
    simular({ isError: true, error: new Error('x') });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('Algo salió mal');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('muestra el estado vacío', () => {
    simular({ data: [] });
    renderizar();
    expect(screen.getByText('Todavía no hay evaluaciones.')).toBeInTheDocument();
  });

  it('muestra el mensaje específico ante un 400', () => {
    simular({ isError: true, error: new ApiError(400, { status: 400, detail: 'Falta la institución.' }) });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('Falta la institución.');
  });

  it('lista las evaluaciones con su estado y un enlace al detalle', () => {
    simular({
      data: [
        evaluacion({}),
        evaluacion({ id: 'e2', titulo: 'TP Funciones', estado: 'Borrador', cohorteId: null }),
      ],
    });
    renderizar();

    expect(screen.getByText('Parcial 1')).toBeInTheDocument();
    expect(screen.getByText('TP Funciones')).toBeInTheDocument();
    expect(screen.getByText('Publicada')).toBeInTheDocument();
    expect(screen.getByText('Borrador')).toBeInTheDocument();
    const enlaces = screen.getAllByRole('link', { name: 'Ver detalle' });
    expect(enlaces).toHaveLength(2);
    expect(enlaces.map((e) => e.getAttribute('href'))).toContain('/evaluaciones/e1');
  });

  it('ordena primero la evaluación actualizada más recientemente', () => {
    simular({
      data: [
        evaluacion({ id: 'vieja', titulo: 'Vieja', fechaActualizacion: '2026-01-01T12:00:00Z' }),
        evaluacion({ id: 'nueva', titulo: 'Nueva', fechaActualizacion: '2026-10-01T12:00:00Z' }),
      ],
    });
    renderizar();
    const filas = screen.getAllByRole('row');
    expect(filas[1]).toHaveTextContent('Nueva');
    expect(filas[2]).toHaveTextContent('Vieja');
  });

  it('tiene un enlace para crear una evaluación nueva', () => {
    simular({ data: [] });
    renderizar();
    expect(screen.getByRole('link', { name: 'Nueva evaluación' })).toHaveAttribute('href', '/evaluaciones/nueva');
  });
});
