import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaCohortes } from './PaginaCohortes';
import { useCurso } from '../hooks/useCurso';
import { useCohortes } from '../hooks/useCohortes';
import { ApiError } from '@/shared/api/cliente';
import { CURSO_1_ID, INSTITUCION_A } from '@/mocks/datos';

vi.mock('../hooks/useCurso', () => ({ useCurso: vi.fn() }));
vi.mock('../hooks/useCohortes', () => ({ useCohortes: vi.fn() }));
vi.mock('../components/FormularioCohorte', () => ({
  FormularioCohorte: () => <div>formulario</div>,
}));

type EstadoCohortes = ReturnType<typeof useCohortes>;

function simular(estado: Partial<EstadoCohortes>) {
  vi.mocked(useCurso).mockReturnValue({
    data: {
      id: CURSO_1_ID,
      institucionId: INSTITUCION_A,
      nombre: 'Programación I',
      area: 'Sistemas',
      activo: true,
      fechaCreacionUtc: '2026-10-09T12:00:00Z',
    },
  } as unknown as ReturnType<typeof useCurso>);
  vi.mocked(useCohortes).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as EstadoCohortes);
}

// La página lee el cursoId de la URL, por eso se renderiza dentro de una ruta
function renderizar() {
  return render(
    <MemoryRouter initialEntries={[`/cursos/${CURSO_1_ID}/cohortes`]}>
      <Routes>
        <Route path='/cursos/:cursoId/cohortes' element={<PaginaCohortes />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('PaginaCohortes', () => {
  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    renderizar();
    expect(screen.getByRole('status')).toHaveTextContent('Cargando cohortes');
  });

  it('muestra el error con el boton de reintento', () => {
    simular({ isError: true, error: new ApiError(500, null) });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('Algo salió mal');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('muestra el estado vacio', () => {
    simular({ data: [] });
    renderizar();
    expect(screen.getByText('Todavía no hay cohortes en este curso.')).toBeInTheDocument();
  });

  it('lista las cohortes con el nombre del curso, el período y el enlace a la matricula', () => {
    simular({
      data: [
        {
          id: 'co-1',
          cursoId: CURSO_1_ID,
          docenteId: '11111111-0000-0000-0000-000000000002',
          nombre: 'Cohorte 2026 - Mañana',
          periodo: '2026-1C',
          activo: true,
          fechaCreacionUtc: '2026-10-09T12:00:00Z',
        },
      ],
    });
    renderizar();
    expect(screen.getByRole('heading', { name: 'Cohortes de Programación I' })).toBeInTheDocument();
    expect(screen.getByText('Cohorte 2026 - Mañana')).toBeInTheDocument();
    expect(screen.getByText('2026-1C')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver matrícula' })).toHaveAttribute(
      'href',
      '/cohortes/co-1/matriculas',
    );
  });
});