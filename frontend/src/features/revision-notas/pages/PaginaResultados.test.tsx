import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useEvaluacion } from '@/features/evaluaciones/hooks/useEvaluacion';
import { useEntregas } from '../hooks/useEntregas';
import { useNombresAlumnos } from '../hooks/useNombresAlumnos';
import type { EntregaResumen } from '../tipos';
import { PaginaResultados } from './PaginaResultados';

vi.mock('@/features/evaluaciones/hooks/useEvaluacion', () => ({ useEvaluacion: vi.fn() }));
vi.mock('../hooks/useEntregas', () => ({ useEntregas: vi.fn(), useEntrega: vi.fn() }));
vi.mock('../hooks/useNombresAlumnos', () => ({ useNombresAlumnos: vi.fn() }));

const base: EntregaResumen = {
  entregaId: 'en1',
  alumnoId: 'a1',
  salaId: 's1',
  estado: 'Corregida',
  respondioAlgo: true,
  notaMultipleChoice: 1,
  puntajeMaximoMultipleChoice: 2,
  notaSugeridaAbiertas: 3.5,
  puntajeMaximoAbiertas: 4,
  fechaEntregaUtc: '2026-11-05T14:35:00Z',
  alertasFoco: { amarillas: 2, rojas: 1 },
};

function simular(estado: Record<string, unknown>, fallaronNombres = false) {
  vi.mocked(useEvaluacion).mockReturnValue({ data: { titulo: 'Parcial 1' } } as unknown as ReturnType<typeof useEvaluacion>);
  vi.mocked(useEntregas).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as ReturnType<typeof useEntregas>);
  vi.mocked(useNombresAlumnos).mockReturnValue({
    nombreDe: (id: string) => (id === 'a1' ? 'Pérez, Ana' : `Alumno ···${id}`),
    cargando: false,
    fallo: fallaronNombres,
  });
}

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/evaluaciones/e1/resultados']}>
      <Routes>
        <Route path='/evaluaciones/:evaluacionId/resultados' element={<PaginaResultados />} />
      </Routes>
    </MemoryRouter>,
  );
}

const fila = (nombre: string) => screen.getByRole('link', { name: nombre }).closest('tr') as HTMLElement;

describe('PaginaResultados', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el título de la evaluación y las pestañas con «Resultados» activa', () => {
    simular({ data: [] });
    renderizar();
    expect(screen.getByRole('heading', { level: 1, name: 'Parcial 1' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Resultados' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Detalle' })).toHaveAttribute('href', '/evaluaciones/e1');
  });

  it('muestra una fila por alumno con nombre, estado, notas y alertas', () => {
    simular({ data: [base] });
    renderizar();
    const f = within(fila('Pérez, Ana'));
    expect(f.getByText('Corregida')).toBeInTheDocument();
    expect(f.getByText('1 / 2')).toBeInTheDocument();
    expect(f.getByText('3,5 / 4')).toBeInTheDocument();
    expect(f.getByText('Sugerida')).toBeInTheDocument();
    expect(f.getByText('1 roja')).toBeInTheDocument();
    expect(f.getByText('2 amarillas')).toBeInTheDocument();
  });

  it('el nombre del alumno lleva al detalle de la entrega', () => {
    simular({ data: [base] });
    renderizar();
    expect(screen.getByRole('link', { name: 'Pérez, Ana' })).toHaveAttribute('href', '/evaluaciones/e1/resultados/en1');
  });

  it('sin nota de la IA todavía muestra «Pendiente» y sin etiqueta «Sugerida»', () => {
    simular({
      data: [{ ...base, estado: 'PendienteCorreccion', notaSugeridaAbiertas: null, alertasFoco: { amarillas: 0, rojas: 0 } }],
    });
    renderizar();
    const f = within(fila('Pérez, Ana'));
    expect(f.getByText('Pendiente de corrección')).toBeInTheDocument();
    expect(f.getByText('Pendiente')).toBeInTheDocument();
    expect(f.queryByText('Sugerida')).not.toBeInTheDocument();
    expect(f.getByText('Sin alertas')).toBeInTheDocument();
  });

  it('si el examen no tiene preguntas abiertas o de opción múltiple muestra un guion', () => {
    simular({
      data: [
        {
          ...base,
          notaMultipleChoice: null,
          puntajeMaximoMultipleChoice: null,
          notaSugeridaAbiertas: null,
          puntajeMaximoAbiertas: null,
        },
      ],
    });
    renderizar();
    expect(within(fila('Pérez, Ana')).getAllByText('—')).toHaveLength(2);
  });

  it('usa el singular con una sola alerta de cada tipo', () => {
    simular({ data: [{ ...base, alertasFoco: { amarillas: 1, rojas: 1 } }] });
    renderizar();
    expect(screen.getByText('1 amarilla')).toBeInTheDocument();
    expect(screen.getByText('1 roja')).toBeInTheDocument();
  });

  it('marca a quien no respondió nada', () => {
    simular({ data: [{ ...base, respondioAlgo: false }] });
    renderizar();
    expect(screen.getByText('No respondió')).toBeInTheDocument();
  });

  it('a quien no respondió nada no le muestra «0 / 4» como nota de la IA', () => {
    simular({ data: [{ ...base, respondioAlgo: false, notaSugeridaAbiertas: 0 }] });
    renderizar();
    expect(screen.getByText('Sin respuestas')).toBeInTheDocument();
    expect(screen.queryByText(/^0 \/ 4/)).not.toBeInTheDocument();
    expect(screen.queryByText('Sugerida')).not.toBeInTheDocument();
  });

  it('avisa cuando todavía no hay entregas', () => {
    simular({ data: [] });
    renderizar();
    expect(screen.getByText('Todavía nadie entregó este examen.')).toBeInTheDocument();
  });

  it('aclara que la nota de la IA es solo una sugerencia', () => {
    simular({ data: [base] });
    renderizar();
    expect(screen.getByText(/no es la nota final/)).toBeInTheDocument();
  });

  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    renderizar();
    expect(screen.getByText('Cargando los resultados…')).toBeInTheDocument();
  });

  it('ante un error ofrece reintentar', async () => {
    const refetch = vi.fn();
    simular({ isError: true, error: new ApiError(500, { status: 500, title: 'Falló' }), refetch });
    renderizar();
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it('si no se pudieron cargar los nombres lo dice y la tabla sigue funcionando', () => {
    simular({ data: [base] }, true);
    renderizar();
    expect(screen.getByText(/No pudimos cargar los nombres/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pérez, Ana' })).toBeInTheDocument();
  });
});
