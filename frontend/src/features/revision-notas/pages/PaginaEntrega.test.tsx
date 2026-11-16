import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useEntrega } from '../hooks/useEntregas';
import { useNombresAlumnos } from '../hooks/useNombresAlumnos';
import type { EntregaDetalle } from '../tipos';
import { PaginaEntrega } from './PaginaEntrega';

vi.mock('../hooks/useEntregas', () => ({ useEntregas: vi.fn(), useEntrega: vi.fn() }));
vi.mock('../hooks/useNombresAlumnos', () => ({ useNombresAlumnos: vi.fn() }));

const entrega: EntregaDetalle = {
  entregaId: 'en1',
  alumnoId: 'a1',
  salaId: 's1',
  estado: 'Corregida',
  respondioAlgo: true,
  notaMultipleChoice: 2,
  puntajeMaximoMultipleChoice: 2,
  notaSugeridaAbiertas: 3.5,
  puntajeMaximoAbiertas: 4,
  fechaEntregaUtc: '2026-11-05T14:35:00Z',
  alertasFoco: { amarillas: 1, rojas: 1 },
  respuestas: [
    {
      preguntaId: 'p1',
      enunciado: '¿Cuánto es 1 + 1?',
      tipo: 'OpcionMultiple',
      puntajeMaximo: 2,
      respuesta: 'o1',
      opcionElegidaTexto: 'Dos',
      esCorrecta: true,
      puntajeObtenido: 2,
      estadoCorreccion: 'Automatica',
    },
    {
      preguntaId: 'p2',
      enunciado: 'Explicá qué es una variable',
      tipo: 'Abierta',
      puntajeMaximo: 4,
      respuesta: 'Un lugar donde se guarda un valor.',
      notaSugerida: 3.5,
      justificacionIA: 'Explica bien, falta un ejemplo.',
      desglosePorCriterio: [{ criterio: 'Define variable', puntaje: 3.5, comentario: 'Bien' }],
      estadoCorreccion: 'SugeridaIA',
    },
  ],
  alertas: [
    { tipoEvento: 'visibilitychange', duracionMs: 65000, severidad: 'Roja', registradoUtc: '2026-11-05T14:25:00Z' },
    { tipoEvento: 'blur', duracionMs: 900, severidad: 'Amarillo', registradoUtc: '2026-11-05T14:12:00Z' },
  ],
};

function simular(estado: Record<string, unknown>) {
  vi.mocked(useEntrega).mockReturnValue({
    data: undefined,
    isPending: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...estado,
  } as unknown as ReturnType<typeof useEntrega>);
  vi.mocked(useNombresAlumnos).mockReturnValue({ nombreDe: () => 'Pérez, Ana', cargando: false, fallo: false });
}

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/evaluaciones/e1/resultados/en1']}>
      <Routes>
        <Route path='/evaluaciones/:evaluacionId/resultados/:entregaId' element={<PaginaEntrega />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('PaginaEntrega', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el alumno, el estado y el enlace para volver a los resultados', () => {
    simular({ data: entrega });
    renderizar();
    expect(screen.getByRole('heading', { level: 1, name: 'Pérez, Ana' })).toBeInTheDocument();
    expect(screen.getByText('Corregida')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Volver a resultados' })).toHaveAttribute(
      'href',
      '/evaluaciones/e1/resultados',
    );
  });

  it('muestra la opción elegida y si fue correcta en la pregunta de opción múltiple', () => {
    simular({ data: entrega });
    renderizar();
    const tarjeta = screen.getByText(/¿Cuánto es 1 \+ 1\?/).closest('li') as HTMLElement;
    expect(within(tarjeta).getByText('Dos')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/\(correcta\)/)).toBeInTheDocument();
  });

  it('en la pregunta abierta muestra la respuesta, la nota sugerida y la justificación de la IA', () => {
    simular({ data: entrega });
    renderizar();
    const tarjeta = screen.getByText(/Explicá qué es una variable/).closest('li') as HTMLElement;
    expect(within(tarjeta).getByText('Un lugar donde se guarda un valor.')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/Nota de la IA: 3,5 \/ 4/)).toBeInTheDocument();
    expect(within(tarjeta).getByText('Sugerida')).toBeInTheDocument();
    expect(within(tarjeta).getByText('Explica bien, falta un ejemplo.')).toBeInTheDocument();
    expect(within(tarjeta).getByText('Define variable')).toBeInTheDocument();
  });

  it('en el desglose por criterio el puntaje usa coma decimal, como el resto de la pantalla', () => {
    simular({ data: entrega });
    renderizar();
    const tarjeta = screen.getByText(/Explicá qué es una variable/).closest('li') as HTMLElement;
    expect(within(tarjeta).getByText('3,5')).toBeInTheDocument();
    expect(within(tarjeta).queryByText('3.5')).not.toBeInTheDocument();
  });

  it('sin respuestas, las abiertas dicen «Sin respuestas» y no una nota sugerida', () => {
    simular({ data: { ...entrega, respondioAlgo: false, notaSugeridaAbiertas: 0 } });
    renderizar();
    expect(screen.getByText('Sin respuestas')).toBeInTheDocument();
  });

  it('avisa si la IA todavía no corrigió una respuesta', () => {
    const pendiente: EntregaDetalle = {
      ...entrega,
      respuestas: [{ ...entrega.respuestas[1], notaSugerida: null, justificacionIA: null, estadoCorreccion: 'PendienteIA' }],
    };
    simular({ data: pendiente });
    renderizar();
    expect(screen.getByText('La IA todavía no corrigió esta respuesta.')).toBeInTheDocument();
    expect(screen.queryByText(/Nota de la IA:/)).not.toBeInTheDocument();
  });

  it('indica cuando no respondió una pregunta', () => {
    const sinResponder: EntregaDetalle = {
      ...entrega,
      respuestas: [{ ...entrega.respuestas[0], respuesta: null, opcionElegidaTexto: null, esCorrecta: null, puntajeObtenido: null }],
    };
    simular({ data: sinResponder });
    renderizar();
    expect(screen.getByText('No respondió.')).toBeInTheDocument();
  });

  it('lista las salidas de la pantalla con su gravedad y duración, y aclara que no cambian la nota', () => {
    simular({ data: entrega });
    renderizar();
    const tarjeta = screen.getByRole('heading', { name: 'Salidas de la pantalla' }).closest('section') as HTMLElement;
    expect(within(tarjeta).getByText('Roja')).toBeInTheDocument();
    expect(within(tarjeta).getByText('1 min 5 s')).toBeInTheDocument();
    expect(within(tarjeta).getByText('Amarilla')).toBeInTheDocument();
    expect(within(tarjeta).getByText('menos de 1 s')).toBeInTheDocument();
    expect(within(tarjeta).getByText(/no modifica la nota/)).toBeInTheDocument();
  });

  it('sin alertas no muestra la tarjeta de salidas de la pantalla', () => {
    simular({ data: { ...entrega, alertas: [], alertasFoco: { amarillas: 0, rojas: 0 } } });
    renderizar();
    expect(screen.queryByRole('heading', { name: 'Salidas de la pantalla' })).not.toBeInTheDocument();
  });

  it('ante un error ofrece reintentar', async () => {
    const refetch = vi.fn();
    simular({ isError: true, error: new ApiError(404, { status: 404, title: 'Entrega no encontrada.' }), refetch });
    renderizar();
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });
});
