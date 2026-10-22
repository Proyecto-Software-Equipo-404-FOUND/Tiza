import { render, screen } from '@testing-library/react';
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

describe('PaginaCursos', () => {
  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    render(<PaginaCursos />);
    expect(screen.getByRole('status')).toHaveTextContent('Cargando cursos');
  });

  it('muestra el error con el boton de reintento', () => {
    simular({ isError: true, error: new ApiError(500, null) });
    render(<PaginaCursos />);
    expect(screen.getByRole('alert')).toHaveTextContent('Algo salió mal');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('muestra el estado vacio', () => {
    simular({ data: [] });
    render(<PaginaCursos />);
    expect(screen.getByText('Todavía no hay cursos.')).toBeInTheDocument();
  });

  it('lista los cursos', () => {
    simular({ data: [{ id: 'c-1', nombre: 'Programación I', institucionId: 'institucion-a' }] });
    render(<PaginaCursos />);
    expect(screen.getByText('Programación I')).toBeInTheDocument();
  });
});