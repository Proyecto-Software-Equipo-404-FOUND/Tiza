import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useMiExamen } from './hooks/useMiExamen';
import type { MiExamenDetalle } from './tipos';
import { PaginaMiExamen } from './PaginaMiExamen';

vi.mock('./hooks/useMiExamen', () => ({ useMiExamen: vi.fn() }));

const examen: MiExamenDetalle = {
  evaluacionId: 'ev1',
  entregaId: 'en1',
  titulo: 'Parcial 1',
  fechaEntregaUtc: '2026-11-05T14:35:00Z',
  notaFinal: 5,
  puntajeMaximo: 10,
  preguntas: [
    {
      preguntaId: 'p1',
      enunciado: '¿Cuánto es 2+2?',
      tipo: 'OpcionMultiple',
      puntajeMaximo: 2,
      opcionElegidaTexto: '5',
      opcionCorrectaTexto: '4',
      esCorrecta: false,
      puntajeObtenido: 0,
    },
    {
      preguntaId: 'p2',
      enunciado: 'Explicá la fotosíntesis',
      tipo: 'Abierta',
      puntajeMaximo: 4,
      respuesta: 'Las plantas usan luz',
      puntajeObtenido: 3,
      justificacion: 'Faltó mencionar el CO2.',
    },
    {
      preguntaId: 'p3',
      enunciado: '¿Capital de Francia?',
      tipo: 'OpcionMultiple',
      puntajeMaximo: 2,
      opcionElegidaTexto: 'París',
      opcionCorrectaTexto: 'París',
      esCorrecta: true,
      puntajeObtenido: 2,
    },
  ],
};

function simular(estado: Record<string, unknown>) {
  vi.mocked(useMiExamen).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as ReturnType<typeof useMiExamen>);
}

const mostrar = () =>
  render(
    <MemoryRouter initialEntries={['/mis-examenes/en1']}>
      <Routes>
        <Route path='/mis-examenes/:entregaId' element={<PaginaMiExamen />} />
      </Routes>
    </MemoryRouter>,
  );

describe('PaginaMiExamen', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('pide el examen de la URL', () => {
    simular({ isPending: true });
    mostrar();
    expect(useMiExamen).toHaveBeenCalledWith('en1');
  });

  it('muestra la nota, cada respuesta y la correcta cuando se equivocó', () => {
    simular({ data: examen });
    mostrar();
    expect(screen.getByRole('heading', { level: 1, name: 'Parcial 1' })).toBeInTheDocument();
    expect(screen.getByText('5 / 10')).toBeInTheDocument();
    expect(screen.getByText('0 / 2')).toBeInTheDocument();
    expect(screen.getByText('(incorrecta)')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByText('Las plantas usan luz')).toBeInTheDocument();
    expect(screen.getByText('Faltó mencionar el CO2.')).toBeInTheDocument();
    expect(screen.getByText('(correcta)')).toBeInTheDocument();
    // Si acertó no se repite la respuesta correcta
    expect(screen.getAllByText(/Respuesta correcta/)).toHaveLength(1);
  });

  it('si la nota no está aprobada avisa que falta la corrección', () => {
    simular({ isError: true, error: new ApiError(409, null) });
    mostrar();
    expect(screen.getByRole('alert')).toHaveTextContent('Tu docente todavía no terminó de corregir este examen.');
  });
});
