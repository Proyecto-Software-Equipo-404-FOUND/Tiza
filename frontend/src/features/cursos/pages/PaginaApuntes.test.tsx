import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi } from 'vitest';
import { PaginaApuntes } from './PaginaApuntes';
import { useCurso } from '../hooks/useCurso';
import { useMateriales } from '../hooks/useMateriales';
import { useEliminarMaterial } from '../hooks/useEliminarMaterial';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { etiquetaEstado, formatearTamano, type Material } from '../apuntes';
import { CURSO_1_ID, DOCENTE_A_ID, INSTITUCION_A } from '@/mocks/datos';

vi.mock('../hooks/useCurso', () => ({ useCurso: vi.fn() }));
vi.mock('../hooks/useMateriales', () => ({ useMateriales: vi.fn() }));
vi.mock('../hooks/useEliminarMaterial', () => ({ useEliminarMaterial: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: vi.fn() }));
vi.mock('../components/FormularioApunte', () => ({
  FormularioApunte: () => <div>formulario</div>,
}));

type EstadoMateriales = ReturnType<typeof useMateriales>;

const material: Material = {
  id: 'dddddddd-0000-0000-0000-000000000001',
  cursoId: CURSO_1_ID,
  titulo: 'Unidad 1 - Introducción',
  nombreArchivo: 'unidad-1.pdf',
  tamanoBytes: 1_250_000,
  cantidadPaginas: 18,
  estado: 'Listo',
  subidoPorId: DOCENTE_A_ID,
  fechaSubidaUtc: '2026-10-20T12:00:00Z',
};

function simular(estado: Partial<EstadoMateriales>) {
  const mutar = vi.fn();
  const mostrar = vi.fn();
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
  vi.mocked(useMateriales).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as EstadoMateriales);
  vi.mocked(useEliminarMaterial).mockReturnValue({
    mutate: mutar,
    isPending: false,
  } as unknown as ReturnType<typeof useEliminarMaterial>);
  vi.mocked(useAviso).mockReturnValue({ mostrar } as unknown as ReturnType<typeof useAviso>);
  return { mutar, mostrar };
}

// La página lee el cursoId de la URL, por eso se renderiza dentro de una ruta
function renderizar() {
  return render(
    <MemoryRouter initialEntries={[`/cursos/${CURSO_1_ID}/apuntes`]}>
      <Routes>
        <Route path='/cursos/:cursoId/apuntes' element={<PaginaApuntes />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('PaginaApuntes', () => {
  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    renderizar();
    expect(screen.getByRole('status')).toHaveTextContent('Cargando apuntes');
  });

  it('muestra el error con el botón de reintento', () => {
    simular({ isError: true, error: new ApiError(500, null) });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent('Algo salió mal');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('en un 403 muestra el mensaje del servidor, sin reintento y sin el formulario de subida', () => {
    simular({
      isError: true,
      error: new ApiError(403, {
        title: 'No tenés permiso para gestionar el material de este curso.',
        status: 403,
      }),
    });
    renderizar();
    expect(screen.getByRole('alert')).toHaveTextContent(
      'No tenés permiso para gestionar el material de este curso.',
    );
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Subir apunte' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Volver a cursos' })).toBeInTheDocument();
  });

  it('muestra el estado vacío y el formulario de subida', () => {
    simular({ data: [] });
    renderizar();
    expect(screen.getByText('Todavía no hay apuntes en este curso.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Subir apunte' })).toBeInTheDocument();
    expect(screen.getByText('formulario')).toBeInTheDocument();
  });

  it('lista los apuntes con título, páginas, tamaño, fecha y estado', () => {
    simular({ data: [material] });
    renderizar();
    expect(screen.getByRole('heading', { name: 'Apuntes de Programación I' })).toBeInTheDocument();
    expect(screen.getByText('Unidad 1 - Introducción')).toBeInTheDocument();
    expect(screen.getByText('18')).toBeInTheDocument();
    expect(screen.getByText(formatearTamano(material.tamanoBytes))).toBeInTheDocument();
    expect(
      screen.getByText(new Date(material.fechaSubidaUtc).toLocaleDateString('es-AR')),
    ).toBeInTheDocument();
    expect(screen.getByText(etiquetaEstado(material.estado))).toBeInTheDocument();
  });

  it('eliminar pide confirmación y recién al confirmar borra el apunte', async () => {
    const { mutar } = simular({ data: [material] });
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Unidad 1 - Introducción' }));
    expect(screen.getByRole('dialog', { name: '¿Eliminar «Unidad 1 - Introducción»?' })).toBeInTheDocument();
    expect(mutar).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));
    expect(mutar).toHaveBeenCalledWith(material.id, expect.anything());
  });

  it('al cancelar la confirmación no borra nada', async () => {
    const { mutar } = simular({ data: [material] });
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Unidad 1 - Introducción' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(mutar).not.toHaveBeenCalled();
  });

  it('avisa el éxito del borrado', async () => {
    const { mutar, mostrar } = simular({ data: [material] });
    mutar.mockImplementation((_id: string, opciones: { onSuccess: () => void }) => opciones.onSuccess());
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Unidad 1 - Introducción' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));
    expect(mostrar).toHaveBeenCalledWith('exito', 'Apunte eliminado.');
  });

  it('avisa el error del servidor al fallar el borrado', async () => {
    const { mutar, mostrar } = simular({ data: [material] });
    mutar.mockImplementation((_id: string, opciones: { onError: (e: unknown) => void }) =>
      opciones.onError(new ApiError(404, { title: 'Material no encontrado.', status: 404 })),
    );
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Unidad 1 - Introducción' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));
    expect(mostrar).toHaveBeenCalledWith('error', 'Material no encontrado.');
  });

  it('en un 403 al borrar no repite el aviso, porque ya lo muestra el manejo global', async () => {
      const { mutar, mostrar } = simular({ data: [material] });
      mutar.mockImplementation((_id: string, opciones: { onError: (e: unknown) => void }) =>
        opciones.onError(
          new ApiError(403, {
            title: 'No tenés permiso para gestionar el material de este curso.',
            status: 403,
          }),
        ),
      );
      renderizar();

      await userEvent.click(screen.getByRole('button', { name: 'Eliminar Unidad 1 - Introducción' }));
      await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));
      expect(mostrar).not.toHaveBeenCalled();
    });
});
