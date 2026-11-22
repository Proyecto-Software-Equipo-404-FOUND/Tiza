import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { VistaPreviaApunte, MENSAJE_SIN_TEXTO } from './VistaPreviaApunte';
import { useTextoMaterial } from '../hooks/useTextoMaterial';
import { ApiError } from '@/shared/api/cliente';
import type { Material } from '../apuntes';
import { CURSO_1_ID, DOCENTE_A_ID } from '@/mocks/datos';

vi.mock('../hooks/useTextoMaterial', () => ({ useTextoMaterial: vi.fn() }));

const material: Material = {
  id: 'dddddddd-0000-0000-0000-000000000001',
  cursoId: CURSO_1_ID,
  titulo: 'Unidad 1',
  nombreArchivo: 'unidad-1.pdf',
  tamanoBytes: 1000,
  cantidadPaginas: 2,
  estado: 'Listo',
  subidoPorId: DOCENTE_A_ID,
  fechaSubidaUtc: '2026-10-20T12:00:00Z',
};

type EstadoTexto = ReturnType<typeof useTextoMaterial>;

function simular(estado: Partial<EstadoTexto>) {
  const refetch = vi.fn();
  vi.mocked(useTextoMaterial).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch,
    ...estado,
  } as unknown as EstadoTexto);
  return { refetch };
}

function renderizar(onCerrar = vi.fn()) {
  render(<VistaPreviaApunte material={material} abierto onCerrar={onCerrar} />);
  return { onCerrar };
}

describe('VistaPreviaApunte', () => {
  it('muestra el texto y la cantidad de páginas', () => {
    simular({
      data: { materialId: material.id, titulo: 'Unidad 1', cantidadPaginas: 2, texto: 'Hola mundo', tieneTexto: true },
    });
    renderizar();

    expect(screen.getByRole('dialog', { name: 'Texto de «Unidad 1»' })).toBeInTheDocument();
    expect(screen.getByText('2 páginas')).toBeInTheDocument();
    expect(screen.getByText('Hola mundo')).toBeInTheDocument();
  });

  it('con un PDF sin texto seleccionable avisa en vez de mostrar el texto', () => {
    simular({
      data: { materialId: material.id, titulo: 'Unidad 1', cantidadPaginas: 2, texto: '', tieneTexto: false },
    });
    renderizar();

    expect(screen.getByText(MENSAJE_SIN_TEXTO)).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Texto extraído del apunte' })).not.toBeInTheDocument();
  });

  it('si el servidor no manda tieneTexto, decide por el texto', () => {
    simular({
      data: { materialId: material.id, titulo: 'Unidad 1', cantidadPaginas: 2, texto: '' },
    });
    renderizar();

    expect(screen.getByText(MENSAJE_SIN_TEXTO)).toBeInTheDocument();
  });

  it('muestra el spinner mientras carga', () => {
    simular({ isPending: true });
    renderizar();

    expect(screen.getByRole('status')).toHaveTextContent('Cargando el texto');
  });

  it('en un 409 avisa que se está procesando y deja reintentar', async () => {
    const { refetch } = simular({ isError: true, error: new ApiError(409, null) });
    renderizar();

    expect(screen.getByRole('alert')).toHaveTextContent('El apunte todavía se está procesando');
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(refetch).toHaveBeenCalled();
  });

  it('en un 403 muestra el mensaje del servidor y no ofrece reintentar', () => {
    simular({
      isError: true,
      error: new ApiError(403, {
        title: 'Solo el docente del curso o un administrador pueden ver el texto.',
        status: 403,
      }),
    });
    renderizar();

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Solo el docente del curso o un administrador pueden ver el texto.',
    );
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
  });

  it('en un 404 no ofrece reintentar', () => {
    simular({ isError: true, error: new ApiError(404, { title: 'Material no encontrado.', status: 404 }) });
    renderizar();

    expect(screen.getByRole('alert')).toHaveTextContent('Material no encontrado.');
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
  });

  it('«Cerrar» avisa al padre', async () => {
    simular({ data: { materialId: material.id, titulo: 'Unidad 1', cantidadPaginas: 2, texto: 'x', tieneTexto: true } });
    const { onCerrar } = renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Cerrar' }));
    expect(onCerrar).toHaveBeenCalled();
  });
});
