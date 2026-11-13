import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, it, expect, vi } from 'vitest';
import { PaginaCohortes } from './PaginaCohortes';
import { useEsAdministrador } from '@/features/auth/useEsAdministrador';
import { useCurso } from '../hooks/useCurso';
import { useCohortes } from '../hooks/useCohortes';
import { ApiError } from '@/shared/api/cliente';
import type { Cohorte } from '@/shared/api/tipos';
import { CURSO_1_ID, COHORTE_1_ID, DOCENTE_A_ID, INSTITUCION_A } from '@/mocks/datos';

vi.mock('../hooks/useCurso', () => ({ useCurso: vi.fn() }));
vi.mock('@/features/auth/useEsAdministrador', () => ({ useEsAdministrador: vi.fn() }));
vi.mock('../hooks/useCohortes', () => ({ useCohortes: vi.fn() }));
vi.mock('../components/FormularioCohorte', () => ({
  FormularioCohorte: () => <div>formulario</div>,
}));

vi.mock('../components/AccionesCohorte', () => ({
  AccionesCohorte: ({ cohorte, onEditar }: { cohorte: Cohorte; onEditar: (c: Cohorte) => void }) => (
    <button onClick={() => onEditar(cohorte)}>acciones de {cohorte.nombre}</button>
  ),
}));
vi.mock('../components/FormularioEditarCohorte', () => ({
  FormularioEditarCohorte: ({ cohorte, onTerminar }: { cohorte: Cohorte; onTerminar: () => void }) => (
    <div>
      formulario de edición de {cohorte.nombre}
      <button onClick={onTerminar}>cancelar edición</button>
    </div>
  ),
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
  beforeEach(() => {
    vi.mocked(useEsAdministrador).mockReturnValue(true);
  });

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
          id: COHORTE_1_ID,
          cursoId: CURSO_1_ID,
          docenteId: DOCENTE_A_ID,
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
      `/cohortes/${COHORTE_1_ID}/matriculas`,
    );
  });
  it('al elegir editar abre la ventana de edición y al cancelar se cierra; el formulario de crear sigue visible', async () => {
    simular({
      data: [
        {
          id: COHORTE_1_ID,
          cursoId: CURSO_1_ID,
          docenteId: DOCENTE_A_ID,
          nombre: 'Cohorte 2026 - Mañana',
          periodo: '2026-1C',
          activo: true,
          fechaCreacionUtc: '2026-10-09T12:00:00Z',
        },
      ],
    });
    renderizar();
    expect(screen.getByRole('heading', { name: 'Nueva cohorte' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'acciones de Cohorte 2026 - Mañana' }));
    expect(screen.getByRole('dialog', { name: 'Editar cohorte' })).toBeInTheDocument();
    expect(screen.getByText(/formulario de edición de Cohorte 2026 - Mañana/)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Nueva cohorte' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'cancelar edición' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByRole('heading', { name: 'Nueva cohorte' })).toBeInTheDocument();
  });

  it('a un docente no le ofrece crear, editar ni eliminar cohortes, pero sí ver la matrícula', () => {
    vi.mocked(useEsAdministrador).mockReturnValue(false);
    simular({
      data: [
        {
          id: COHORTE_1_ID,
          cursoId: CURSO_1_ID,
          docenteId: DOCENTE_A_ID,
          nombre: 'Cohorte 2026 - Mañana',
          periodo: '2026-1C',
          activo: true,
          fechaCreacionUtc: '2026-10-09T12:00:00Z',
        },
      ],
    });
    renderizar();

    expect(screen.queryByRole('heading', { name: 'Nueva cohorte' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Acciones' })).not.toBeInTheDocument();
    expect(screen.getByText('Cohorte 2026 - Mañana')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Ver matrícula/ })).toBeInTheDocument();
  });

  it('las cohortes dadas de baja van a la sección de inactivas y no a la lista de activas', () => {
    const base = { cursoId: CURSO_1_ID, docenteId: DOCENTE_A_ID, periodo: '2026-1C', fechaCreacionUtc: '2026-10-09T12:00:00Z' };
    simular({
      data: [
        { ...base, id: COHORTE_1_ID, nombre: 'Cohorte Mañana', activo: true },
        { ...base, id: 'h-baja', nombre: 'Cohorte Noche', activo: false },
      ],
    });
    renderizar();

    const inactivas = screen.getByRole('heading', { name: 'Cohortes inactivas' }).closest('section')!;
    expect(within(inactivas).getByText('Cohorte Noche')).toBeInTheDocument();
    expect(within(inactivas).queryByText('Cohorte Mañana')).not.toBeInTheDocument();
    expect(within(inactivas).queryByRole('button')).not.toBeInTheDocument();
    // En la lista principal solo queda la activa (el único enlace a matrícula es el suyo)
    expect(screen.getAllByRole('link', { name: 'Ver matrícula' })).toHaveLength(1);
  });
});
