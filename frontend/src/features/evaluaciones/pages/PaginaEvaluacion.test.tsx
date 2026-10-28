import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaEvaluacion } from './PaginaEvaluacion';
import { useEvaluacion } from '../hooks/useEvaluacion';
import { ApiError } from '@/shared/api/cliente';
import type { Evaluacion } from '../tipos';

vi.mock('../hooks/useEvaluacion', () => ({ useEvaluacion: vi.fn() }));

type Estado = ReturnType<typeof useEvaluacion>;

function simular(estado: Partial<Estado>) {
  vi.mocked(useEvaluacion).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as Estado);
}

const evaluacion: Evaluacion = {
  id: 'e1',
  institucionId: 'i1',
  titulo: 'Parcial 1',
  tipo: 'Parcial',
  estado: 'Publicada',
  cohorteId: 'c1',
  preguntasJson: {
    version: 1,
    preguntas: [
      {
        id: 'p1',
        enunciado: '¿Cuánto es 1 + 1?',
        tipo: 'OpcionMultiple',
        puntajeMaximo: 2,
        opciones: [
          { id: 'o1', texto: 'Dos', esCorrecta: true },
          { id: 'o2', texto: 'Tres', esCorrecta: false },
        ],
      },
      {
        id: 'p2',
        enunciado: 'Explicá qué es una variable.',
        tipo: 'Abierta',
        puntajeMaximo: 8,
        rubrica: { criterios: [{ descripcion: 'Define el concepto', puntaje: 8 }] },
      },
    ],
  },
  fechaCreacion: '2026-10-20T12:00:00Z',
  fechaActualizacion: '2026-10-22T12:00:00Z',
  version: 3,
};

const renderizar = () =>
  render(
    <MemoryRouter initialEntries={['/evaluaciones/e1']}>
      <Routes>
        <Route path='/evaluaciones/:evaluacionId' element={<PaginaEvaluacion />} />
      </Routes>
    </MemoryRouter>,
  );

describe('PaginaEvaluacion', () => {
  it('pide la evaluación del id que está en la URL', () => {
    simular({ isPending: true });
    renderizar();
    expect(useEvaluacion).toHaveBeenCalledWith('e1');
    expect(screen.getByRole('status')).toHaveTextContent('Cargando la evaluación');
  });

  it('muestra el error cuando no se encuentra la evaluación', () => {
    simular({ isError: true, error: new ApiError(404, { status: 404 }) });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('No se encontró lo que buscás.');
    expect(screen.getByRole('link', { name: '← Volver a evaluaciones' })).toHaveAttribute(
      'href',
      '/evaluaciones',
    );
  });

  it('muestra el título, el estado y la versión', () => {
    simular({ data: evaluacion });
    renderizar();
    expect(screen.getByRole('heading', { level: 1, name: 'Parcial 1' })).toBeInTheDocument();
    expect(screen.getByText('Publicada')).toBeInTheDocument();
    expect(screen.getByText('Versión 3')).toBeInTheDocument();
  });

  it('resume la cantidad de preguntas y el puntaje total', () => {
    simular({ data: evaluacion });
    renderizar();
    expect(screen.getByText(/2 preguntas · 10 puntos en total/)).toBeInTheDocument();
  });

  it('marca la opción correcta de una pregunta de opción múltiple', () => {
    simular({ data: evaluacion });
    renderizar();
    expect(screen.getByText('Dos').closest('li')).toHaveTextContent('(respuesta correcta)');
    expect(screen.getByText('Tres').closest('li')).not.toHaveTextContent('(respuesta correcta)');
  });

  it('muestra la rúbrica de una pregunta abierta', () => {
    simular({ data: evaluacion });
    renderizar();
    const pregunta = screen.getByText(/Explicá qué es una variable/).closest('li') as HTMLElement;
    expect(within(pregunta).getByText('Define el concepto')).toBeInTheDocument();
  });

  it('avisa cuando la evaluación no tiene preguntas', () => {
    simular({ data: { ...evaluacion, preguntasJson: { version: 1, preguntas: [] } } });
    renderizar();
    expect(screen.getByText('Esta evaluación todavía no tiene preguntas.')).toBeInTheDocument();
  });
});
