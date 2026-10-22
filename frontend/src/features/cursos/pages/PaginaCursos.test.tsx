import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaCursos } from './PaginaCursos';
import { useCursos } from '../hooks/useCursos';
import { ApiError } from '@/shared/api/cliente';

vi.mock('../hooks/useCursos', () => ({ useCursos: vi.fn() }));
vi.mock('../components/FormularioCurso', () => ({
  FormularioCurso: () => <div>formulario</div>,
}));

type Estado = ReturnType<typeof useCursos>;

function simular(estado: Partial<Estado>) {
  vi.mocked(useCursos).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as Estado);
}

// La página tiene enlaces a las cohortes, que necesitan un router
function renderizar() {
  return render(
    <MemoryRouter>
      <PaginaCursos />
    </MemoryRouter>,
  );
}

describe('PaginaCursos', () => {
  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    renderizar();
    expect(screen.getByRole('status')).toHaveTextContent('Cargando cursos');
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
    expect(screen.getByText('Todavía no hay cursos.')).toBeInTheDocument();
  });

  it('lista los cursos', () => {
    simular({ data: [{ id: 'c-1', nombre: 'Programación I', institucionId: 'institucion-a' }] });
    renderizar();
    expect(screen.getByText('Programación I')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver cohortes' })).toHaveAttribute(
      'href',
      '/cursos/c-1/cohortes',
    );
  });
});