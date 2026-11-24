import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useSesion } from '@/features/auth/SesionContext';
import { useAprobarNota } from '../hooks/useAprobarNota';
import { useEntrega } from '../hooks/useEntregas';
import { useNombresAlumnos } from '../hooks/useNombresAlumnos';
import type { EntregaDetalle } from '../tipos';
import { PaginaEntrega } from './PaginaEntrega';

vi.mock('../hooks/useEntregas', () => ({ useEntregas: vi.fn(), useEntrega: vi.fn() }));
vi.mock('../hooks/useNombresAlumnos', () => ({ useNombresAlumnos: vi.fn() }));
vi.mock('../hooks/useAprobarNota', () => ({ useAprobarNota: vi.fn() }));
vi.mock('@/features/auth/SesionContext', () => ({ useSesion: vi.fn() }));

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
    { preguntaId: 'p2', tipoEvento: 'visibilitychange', duracionMs: 65000, severidad: 'Roja', registradoUtc: '2026-11-05T14:25:00Z' },
    { tipoEvento: 'blur', duracionMs: 900, severidad: 'Amarillo', registradoUtc: '2026-11-05T14:12:00Z' },
  ],
};

const aprobarNota = vi.fn();

function simular(estado: Record<string, unknown>, rol: 'docente' | 'administrador' = 'docente') {
  vi.mocked(useSesion).mockReturnValue({
    usuario: { id: 'u1', nombre: 'Usuario', rol, institucionId: 'i1' },
  } as unknown as ReturnType<typeof useSesion>);
  vi.mocked(useAprobarNota).mockReturnValue({
    mutate: aprobarNota,
    isPending: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useAprobarNota>);
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

const arbol = () => (
  <MemoryRouter initialEntries={['/evaluaciones/e1/resultados/en1']}>
    <Routes>
      <Route path='/evaluaciones/:evaluacionId/resultados/:entregaId' element={<PaginaEntrega />} />
    </Routes>
  </MemoryRouter>
);

function renderizar() {
  return render(arbol());
}

describe('PaginaEntrega', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('muestra el alumno, el estado y el enlace para volver a los resultados', () => {
    simular({ data: entrega });
    renderizar();
    expect(screen.getByRole('heading', { level: 1, name: 'Pérez, Ana' })).toBeInTheDocument();
    expect(screen.getByText('Nota sugerida')).toBeInTheDocument();
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
    expect(screen.getByText('Sin nota sugerida')).toBeInTheDocument();
    expect(screen.queryByText('Nota sugerida')).not.toBeInTheDocument();
  });

  it('si la pantalla se refresca después de guardar un puntaje, no se pierde lo escrito', async () => {
    simular({ data: entrega });
    const vista = renderizar();
    await userEvent.clear(screen.getByLabelText(/Nota final de la pregunta 2/));
    await userEvent.type(screen.getByLabelText(/Nota final de la pregunta 2/), '4');
    // Un guardado salió bien y el refresco trae la nota como «Editada»
    simular({ data: { ...entrega, estadoNota: 'Editada' } });
    vista.rerender(arbol());
    expect(screen.getByLabelText(/Nota final de la pregunta 2/)).toHaveValue('4');
  });

  it('el texto de «Notas» cambia según quién mira y si ya está corregida', () => {
    simular({ data: entrega });
    const vista = renderizar();
    expect(screen.getByText(/es la que confirmás más abajo/)).toBeInTheDocument();
    vista.unmount();
    simular({ data: entrega }, 'administrador');
    const admin = renderizar();
    expect(screen.getByText(/la define el docente/)).toBeInTheDocument();
    admin.unmount();
    simular({ data: { ...entrega, estadoNota: 'Aprobada', notaFinal: 5 } });
    renderizar();
    expect(screen.getByText(/ya está corregida/)).toBeInTheDocument();
  });

  it('con la nota aprobada muestra cuándo se corrigió, y sin fecha no inventa una', () => {
    simular({
      data: { ...entrega, estadoNota: 'Aprobada', notaFinal: 5, fechaAprobacionUtc: '2026-11-20T15:30:00Z' },
    });
    const vista = renderizar();
    // La fecha va entre paréntesis: la hora termina en punto («a. m.») y no debe quedar un doble punto
    const subtitulo = screen.getByText(/Nota corregida \(.*20\/11\/26.*\)\. El alumno ya puede verla\./);
    expect(subtitulo).toBeInTheDocument();
    expect(subtitulo.textContent).not.toContain('..');
    vista.unmount();
    simular({ data: { ...entrega, estadoNota: 'Aprobada', notaFinal: 5 } });
    renderizar();
    expect(screen.getByText('Nota corregida. El alumno ya puede verla.')).toBeInTheDocument();
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

  it('indica en qué pregunta se salió: columna en la tabla y aviso dentro de la respuesta', () => {
    simular({ data: entrega });
    renderizar();
    const tarjeta = screen.getByRole('heading', { name: 'Salidas de la pantalla' }).closest('section') as HTMLElement;
    expect(within(tarjeta).getByText('Pregunta 2')).toBeInTheDocument();
    expect(within(tarjeta).getByText('Fuera de una pregunta')).toBeInTheDocument();
    expect(screen.getByText('Salió 1 vez de la pantalla · 1 min 5 s en total')).toBeInTheDocument();
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

  describe('nota final', () => {
    it('precarga cada pregunta abierta con la nota sugerida y suma el total', () => {
      simular({ data: entrega });
      renderizar();
      expect(screen.getByLabelText(/Nota final de la pregunta 2/)).toHaveValue('3,5');
      expect(screen.getByText('5,5 / 6')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Finalizar corrección' })).toBeEnabled();
    });

    it('al cambiar el puntaje recalcula el total', async () => {
      simular({ data: entrega });
      renderizar();
      const campo = screen.getByLabelText(/Nota final de la pregunta 2/);
      await userEvent.clear(campo);
      await userEvent.type(campo, '4');
      expect(screen.getByText('6 / 6')).toBeInTheDocument();
    });

    it('no deja aprobar con un puntaje que supera el máximo y lo explica', async () => {
      simular({ data: entrega });
      renderizar();
      const campo = screen.getByLabelText(/Nota final de la pregunta 2/);
      await userEvent.clear(campo);
      await userEvent.type(campo, '5');
      expect(screen.getByText('No puede superar 4.')).toBeInTheDocument();
      expect(screen.getByText('Faltan puntajes')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Finalizar corrección' })).toBeDisabled();
    });

    it('si la IA no sugirió nota, hay que completarla a mano para poder aprobar', async () => {
      const pendiente: EntregaDetalle = {
        ...entrega,
        respuestas: [entrega.respuestas[0], { ...entrega.respuestas[1], notaSugerida: null, estadoCorreccion: 'PendienteIA' }],
      };
      simular({ data: pendiente });
      renderizar();
      expect(screen.getByText('Hay 1 pregunta sin puntaje válido. Completalas para poder finalizar la corrección.')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Finalizar corrección' })).toBeDisabled();
      await userEvent.type(screen.getByLabelText(/Nota final de la pregunta 2/), '2,5');
      expect(screen.getByRole('button', { name: 'Finalizar corrección' })).toBeEnabled();
    });

    it('si el docente solo confirma lo que sugirió la IA, aprueba sin guardar cambios', async () => {
      simular({ data: entrega });
      renderizar();
      await userEvent.click(screen.getByRole('button', { name: 'Finalizar corrección' }));
      expect(aprobarNota).not.toHaveBeenCalled();
      await userEvent.click(screen.getByRole('button', { name: 'Sí, finalizar' }));
      expect(aprobarNota).toHaveBeenCalledWith([]);
    });

    it('manda solo los puntajes que el docente cambió', async () => {
      simular({ data: entrega });
      renderizar();
      const campo = screen.getByLabelText(/Nota final de la pregunta 2/);
      await userEvent.clear(campo);
      await userEvent.type(campo, '4');
      await userEvent.click(screen.getByRole('button', { name: 'Finalizar corrección' }));
      await userEvent.click(screen.getByRole('button', { name: 'Sí, finalizar' }));
      expect(aprobarNota).toHaveBeenCalledWith([{ preguntaId: 'p2', puntaje: 4 }]);
    });

    it('con la nota ya aprobada no se puede editar ni volver a aprobar', () => {
      simular({
        data: {
          ...entrega,
          notaFinal: 5.5,
          estadoNota: 'Aprobada',
          respuestas: [entrega.respuestas[0], { ...entrega.respuestas[1], puntajeDocente: 3.5 }],
        },
      });
      renderizar();
      // El estado de corrección deja paso a «Corregida»
      expect(screen.getAllByText('Corregida').length).toBeGreaterThan(0);
      expect(screen.queryByText('Nota sugerida')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Finalizar corrección' })).not.toBeInTheDocument();
      expect(screen.getByLabelText(/Nota final de la pregunta 2/)).toBeDisabled();
    });

    it('el administrador ve la nota pero no puede editarla ni aprobarla', () => {
      simular({ data: entrega }, 'administrador');
      renderizar();
      expect(screen.queryByRole('button', { name: 'Finalizar corrección' })).not.toBeInTheDocument();
      expect(screen.getByLabelText(/Nota final de la pregunta 2/)).toBeDisabled();
      expect(screen.getByText(/Solo el docente que creó el examen puede finalizar la corrección/)).toBeInTheDocument();
    });

    it('explica con el código del contrato por qué no se pudo aprobar', () => {
      simular({ data: entrega });
      vi.mocked(useAprobarNota).mockReturnValue({
        mutate: aprobarNota,
        isPending: false,
        isError: true,
        error: new ApiError(409, { status: 409, type: 'FALTAN_CORRECCIONES' }),
      } as unknown as ReturnType<typeof useAprobarNota>);
      renderizar();
      expect(screen.getByRole('alert')).toHaveTextContent('Todavía hay preguntas abiertas sin nota');
    });

    it('muestra el error si no se pudo aprobar', () => {
      simular({ data: entrega });
      vi.mocked(useAprobarNota).mockReturnValue({
        mutate: aprobarNota,
        isPending: false,
        isError: true,
        error: new ApiError(500, null),
      } as unknown as ReturnType<typeof useAprobarNota>);
      renderizar();
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
