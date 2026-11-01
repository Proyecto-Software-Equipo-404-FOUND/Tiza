import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaCursos } from './PaginaCursos';
import { useCursos } from '../hooks/useCursos';
import { ApiError } from '@/shared/api/cliente';
import type { Curso } from '@/shared/api/tipos';
import { CURSO_1_ID, INSTITUCION_A } from '@/mocks/datos';

vi.mock('../hooks/useCursos', () => ({ useCursos: vi.fn() }));
vi.mock('../components/FormularioCurso', () => ({
  FormularioCurso: () => <div>formulario</div>,
}));

vi.mock('../components/AccionesCurso', () => ({
  AccionesCurso: ({ curso, onEditar }: { curso: Curso; onEditar: (c: Curso) => void }) => (
    <button onClick={() => onEditar(curso)}>acciones de {curso.nombre}</button>
  ),
}));
vi.mock('../components/FormularioEditarCurso', () => ({
  FormularioEditarCurso: ({ curso, onTerminar }: { curso: Curso; onTerminar: () => void }) => (
    <div>
      formulario de edición de {curso.nombre}
      <button onClick={onTerminar}>cancelar edición</button>
    </div>
  ),
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

function paginado(items: Curso[], totalItems = items.length) {
  return { items, totalItems, pagina: 1, tamanoPagina: 10, totalPaginas: 1 };
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
    simular({ data: paginado([]) });
    renderizar();
    expect(screen.getByText('Todavía no hay cursos.')).toBeInTheDocument();
  });

  it('lista los cursos con su área', () => {
    simular({
      data: paginado([
        {
          id: CURSO_1_ID,
          institucionId: INSTITUCION_A,
          nombre: 'Programación I',
          area: 'Sistemas',
          activo: true,
          fechaCreacionUtc: '2026-10-09T12:00:00Z',
        },
      ]),
    });
    renderizar();
    expect(screen.getByText('Programación I')).toBeInTheDocument();
    expect(screen.getByText('Sistemas')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver cohortes' })).toHaveAttribute(
      'href',
      `/cursos/${CURSO_1_ID}/cohortes`,
    );
  });
  it('al elegir editar abre la ventana de edición y al cancelar se cierra; el formulario de crear sigue visible', async () => {
    simular({
      data: paginado([
        {
          id: CURSO_1_ID,
          institucionId: INSTITUCION_A,
          nombre: 'Programación I',
          area: 'Sistemas',
          activo: true,
          fechaCreacionUtc: '2026-10-09T12:00:00Z',
        },
      ]),
    });
    renderizar();
    expect(screen.getByRole('heading', { name: 'Crear nuevo curso' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'acciones de Programación I' }));
    expect(screen.getByRole('dialog', { name: 'Editar curso' })).toBeInTheDocument();
    expect(screen.getByText(/formulario de edición de Programación I/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Crear nuevo curso' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'cancelar edición' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('heading', { name: 'Crear nuevo curso' })).toBeInTheDocument();
  });
});