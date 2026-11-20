import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useEvaluacion } from '@/features/evaluaciones/hooks/useEvaluacion';
import { useNombresAlumnos } from '@/features/revision-notas/hooks/useNombresAlumnos';
import { useMetricas } from './hooks/useMetricas';
import type { MetricasEvaluacion } from './tipos';
import { PaginaMetricas } from './PaginaMetricas';

vi.mock('@/features/evaluaciones/hooks/useEvaluacion', () => ({ useEvaluacion: vi.fn() }));
vi.mock('@/features/revision-notas/hooks/useNombresAlumnos', () => ({ useNombresAlumnos: vi.fn() }));
vi.mock('./hooks/useMetricas', () => ({ useMetricas: vi.fn() }));

const metricas: MetricasEvaluacion = {
  evaluacionId: 'e1',
  cantidadEntregas: 4,
  cantidadAprobadas: 3,
  puntajeMaximo: 10,
  promedio: 6,
  preguntaMasDificilId: 'p2',
  preguntas: [
    { preguntaId: 'p1', enunciado: 'Pregunta uno', puntajeMaximo: 2, promedio: 1.5, porcentaje: 75 },
    { preguntaId: 'p2', enunciado: 'Pregunta dos', puntajeMaximo: 8, promedio: 3.2, porcentaje: 40 },
  ],
  alumnos: [
    { alumnoId: 'a2', notaFinal: 6, diferenciaConPromedio: 0 },
    { alumnoId: 'a3', notaFinal: 4, diferenciaConPromedio: -2 },
    { alumnoId: 'a1', notaFinal: 8, diferenciaConPromedio: 2 },
  ],
};

function simular(estado: Record<string, unknown>, fallaronNombres = false) {
  vi.mocked(useEvaluacion).mockReturnValue({ data: { titulo: 'Parcial 1' } } as unknown as ReturnType<typeof useEvaluacion>);
  vi.mocked(useMetricas).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as ReturnType<typeof useMetricas>);
  vi.mocked(useNombresAlumnos).mockReturnValue({
    nombreDe: (id: string) => ({ a1: 'Pérez, Ana', a2: 'Gómez, Bruno' })[id] ?? `Alumno ···${id}`,
    cargando: false,
    fallo: fallaronNombres,
  });
}

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/evaluaciones/e1/metricas']}>
      <Routes>
        <Route path='/evaluaciones/:evaluacionId/metricas' element={<PaginaMetricas />} />
      </Routes>
    </MemoryRouter>,
  );
}

const fila = (nombre: string) => screen.getByText(nombre).closest('tr') as HTMLElement;

describe('PaginaMetricas', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el título del examen y las pestañas con «Métricas» activa', () => {
    simular({ data: metricas });
    renderizar();
    expect(screen.getByRole('heading', { level: 1, name: 'Parcial 1' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Métricas' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Resultados' })).toHaveAttribute('href', '/evaluaciones/e1/resultados');
  });

  it('muestra un aviso de carga mientras llegan los datos', () => {
    simular({ isPending: true });
    renderizar();
    expect(screen.getByRole('status')).toHaveTextContent('Cargando las métricas…');
  });

  it('muestra el promedio sobre el puntaje máximo y aclara que solo cuentan las notas corregidas', () => {
    simular({ data: metricas });
    renderizar();
    // «6 / 10» también es la nota de un alumno: se busca solo dentro del dato del promedio
    const promedio = screen.getByText('Promedio del examen').closest('div') as HTMLElement;
    expect(within(promedio).getByText('6 / 10')).toBeInTheDocument();
    expect(within(promedio).getByText('Solo cuentan las notas corregidas')).toBeInTheDocument();
    expect(screen.getByText('3 de 4')).toBeInTheDocument();
  });

  it('muestra la pregunta más difícil con su porcentaje de acierto', () => {
    simular({ data: metricas });
    renderizar();
    const dato = screen.getByText('Pregunta más difícil').closest('div') as HTMLElement;
    expect(within(dato).getByText('Pregunta dos')).toBeInTheDocument();
    expect(within(dato).getByText('40 % de acierto promedio')).toBeInTheDocument();
  });

  it('lista cada pregunta con su barra y marca solo la más difícil', () => {
    simular({ data: metricas });
    renderizar();
    const barras = screen.getAllByRole('progressbar');
    expect(barras).toHaveLength(2);
    expect(barras[0]).toHaveAttribute('aria-valuenow', '75');
    expect(barras[1]).toHaveAttribute('aria-valuenow', '40');
    expect(screen.getAllByText('Más difícil')).toHaveLength(1);
    expect(screen.getByText('1,5 / 2 · 75 %')).toBeInTheDocument();
    expect(screen.getByText('3,2 / 8 · 40 %')).toBeInTheDocument();
  });

  it('muestra a cada alumno contra el promedio, de la nota más alta a la más baja', () => {
    simular({ data: metricas });
    renderizar();
    const filas = screen.getAllByRole('row').slice(1);
    expect(filas[0]).toHaveTextContent('Pérez, Ana');
    expect(filas[1]).toHaveTextContent('Gómez, Bruno');
    expect(filas[2]).toHaveTextContent('Alumno ···a3');
    expect(within(fila('Pérez, Ana')).getByText('8 / 10')).toBeInTheDocument();
    expect(within(fila('Pérez, Ana')).getByText('2 puntos por encima del promedio')).toBeInTheDocument();
    expect(within(fila('Gómez, Bruno')).getByText('En el promedio')).toBeInTheDocument();
    expect(within(fila('Alumno ···a3')).getByText('2 puntos por debajo del promedio')).toBeInTheDocument();
  });

  it('con una pregunta más difícil que no está en la lista no inventa nada', () => {
    simular({ data: { ...metricas, preguntaMasDificilId: 'otra' } });
    renderizar();
    const dato = screen.getByText('Pregunta más difícil').closest('div') as HTMLElement;
    expect(within(dato).getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('Más difícil')).not.toBeInTheDocument();
  });

  it('sin notas corregidas explica que las métricas usan las notas corregidas, sin tablas vacías', () => {
    simular({
      data: { ...metricas, cantidadAprobadas: 0, promedio: null, preguntaMasDificilId: null, preguntas: [], alumnos: [] },
    });
    renderizar();
    expect(screen.getByText('Todavía no hay notas corregidas')).toBeInTheDocument();
    expect(screen.getByText(/Hay 4 entregas para corregir/)).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('sin entregas dice que todavía nadie entregó', () => {
    simular({
      data: { ...metricas, cantidadEntregas: 0, cantidadAprobadas: 0, promedio: null, preguntaMasDificilId: null, preguntas: [], alumnos: [] },
    });
    renderizar();
    expect(screen.getByText('Todavía nadie entregó este examen.')).toBeInTheDocument();
  });

  it('con un 403 muestra el mensaje de permiso', () => {
    simular({ isError: true, error: new ApiError(403, null) });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('No tenés permiso para hacer esto.');
  });

  it('con un 404 avisa que no se encontró y permite reintentar', async () => {
    const refetch = vi.fn();
    simular({ isError: true, error: new ApiError(404, null), refetch });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('No se encontró lo que buscás.');
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(refetch).toHaveBeenCalledOnce();
  });

  it('si fallan los nombres de los alumnos lo avisa y sigue mostrando las métricas', () => {
    simular({ data: metricas }, true);
    renderizar();
    expect(screen.getByText(/No pudimos cargar los nombres de los alumnos/)).toBeInTheDocument();
    expect(screen.getByText('Promedio del examen')).toBeInTheDocument();
  });
});
