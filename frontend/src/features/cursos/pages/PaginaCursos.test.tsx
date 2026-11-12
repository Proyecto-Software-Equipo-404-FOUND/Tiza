import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, it, expect, vi } from 'vitest';
import { PaginaCursos } from './PaginaCursos';
import { useEsAdministrador } from '@/features/auth/useEsAdministrador';
import { useCursos } from '../hooks/useCursos';
import { ApiError } from '@/shared/api/cliente';
import type { Curso } from '@/shared/api/tipos';
import { CURSO_1_ID, INSTITUCION_A } from '@/mocks/datos';

vi.mock('../hooks/useCursos', () => ({ useCursos: vi.fn() }));
vi.mock('@/features/auth/useEsAdministrador', () => ({ useEsAdministrador: vi.fn() }));
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
  beforeEach(() => {
    vi.mocked(useEsAdministrador).mockReturnValue(true);
  });

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

  it('a un docente no le ofrece crear, editar ni eliminar cursos, pero sí ver cohortes', () => {
    vi.mocked(useEsAdministrador).mockReturnValue(false);
    simular({ data: paginado([{ id: CURSO_1_ID, institucionId: INSTITUCION_A, nombre: 'Programación I', area: 'Sistemas', activo: true, fechaCreacionUtc: '2026-10-09T12:00:00Z' }]) });
    renderizar();

    expect(screen.queryByRole('heading', { name: 'Crear nuevo curso' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Acciones' })).not.toBeInTheDocument();
    expect(screen.queryByText(/acciones de Programación I/)).not.toBeInTheDocument();
    expect(screen.getByText('Programación I')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Ver cohortes/ })).toBeInTheDocument();
  });

  it('a un administrador le ofrece crear y la columna de acciones', () => {
    simular({ data: paginado([{ id: CURSO_1_ID, institucionId: INSTITUCION_A, nombre: 'Programación I', area: 'Sistemas', activo: true, fechaCreacionUtc: '2026-10-09T12:00:00Z' }]) });
    renderizar();

    expect(screen.getByRole('heading', { name: 'Crear nuevo curso' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Acciones' })).toBeInTheDocument();
  });

  it('los cursos dados de baja van a la sección de inactivos y no a la lista de activos', () => {
    const base = { institucionId: INSTITUCION_A, area: 'Sistemas', fechaCreacionUtc: '2026-10-09T12:00:00Z' };
    simular({
      data: paginado([
        { ...base, id: CURSO_1_ID, nombre: 'Programación I', activo: true },
        { ...base, id: 'c-baja', nombre: 'Algoritmos', activo: false },
      ]),
    });
    renderizar();

    const activos = screen.getByRole('heading', { name: 'Tus cursos activos' }).closest('section')!;
    const inactivos = screen.getByRole('heading', { name: 'Cursos inactivos' }).closest('section')!;
    expect(within(activos).getByText('Programación I')).toBeInTheDocument();
    expect(within(activos).queryByText('Algoritmos')).not.toBeInTheDocument();
    expect(within(inactivos).getByText('Algoritmos')).toBeInTheDocument();
    // Los inactivos son solo lectura
    expect(within(inactivos).queryByRole('button')).not.toBeInTheDocument();
    expect(within(inactivos).queryByRole('link')).not.toBeInTheDocument();
  });
});
