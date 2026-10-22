import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaCohortes } from './PaginaCohortes';
import { useCursos } from '../hooks/useCursos';
import { useCohortes } from '../hooks/useCohortes';
import { ApiError } from '@/shared/api/cliente';

vi.mock('../hooks/useCursos', () => ({ useCursos: vi.fn() }));
vi.mock('../hooks/useCohortes', () => ({ useCohortes: vi.fn() }));
vi.mock('../components/FormularioCohorte', () => ({
  FormularioCohorte: () => <div>formulario</div>,
}));

type EstadoCohortes = ReturnType<typeof useCohortes>;

function simular(estado: Partial<EstadoCohortes>) {
  vi.mocked(useCursos).mockReturnValue({
    data: [{ id: 'c-1', nombre: 'Programación I', institucionId: 'institucion-a' }],
  } as unknown as ReturnType<typeof useCursos>);
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
    <MemoryRouter initialEntries={['/cursos/c-1/cohortes']}>
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

  it('lista las cohortes con el nombre del curso y el enlace a la matricula', () => {
    simular({
      data: [{ id: 'co-1', cursoId: 'c-1', nombre: 'Cohorte 2026 - Mañana', docenteId: 'u-docente-a' }],
    });
    renderizar();
    expect(screen.getByRole('heading', { name: 'Cohortes de Programación I' })).toBeInTheDocument();
    expect(screen.getByText('Cohorte 2026 - Mañana')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver matrícula' })).toHaveAttribute(
      'href',
      '/cohortes/co-1/matriculas',
    );
  });
});