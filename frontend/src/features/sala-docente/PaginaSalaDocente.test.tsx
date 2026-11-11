import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SesionProvider } from '@/features/auth/SesionContext';
import { crearTokenFalso } from '@/mocks/token';
import { ApiError, guardarToken } from '@/shared/api/cliente';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { guardarSala } from './almacenSala';
import { useAbrirSala } from './hooks/useAbrirSala';
import { PaginaSalaDocente } from './PaginaSalaDocente';
import type { SalaCreada } from './tipos';

vi.mock('./hooks/useAbrirSala', () => ({ useAbrirSala: vi.fn() }));

const docente = { id: 'u-docente-a', nombre: 'Docente A', rol: 'docente', institucionId: 'institucion-a' };
const EVALUACION = 'e1';
const SALA_ID = '11111111-1111-4111-8111-111111111111';

const salaVigente = (): SalaCreada => ({
  id: SALA_ID,
  codigoPIN: 654321,
  fechaVencimiento: new Date(Date.now() + 2 * 60 * 60 * 1000).toISOString(),
  capacidadMaxima: 40,
});

const enSegundos = (s: number) => new Date(Date.now() + s * 1000).toISOString();

const pregunta = (indice: number) => ({
  salaId: SALA_ID,
  preguntaId: `bbbbbbbb-0000-4000-8000-00000000000${indice}`,
  indice,
  totalPreguntas: 3,
  tipo: 'Abierta',
  enunciado: `Pregunta ${indice}`,
  opciones: null,
  puntajeMaximo: 2,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(90),
});

function simularAbrir(escritura: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  const reset = vi.fn();
  vi.mocked(useAbrirSala).mockReturnValue({
    mutate,
    reset,
    isPending: false,
    isError: false,
    error: null,
    ...escritura,
  } as unknown as ReturnType<typeof useAbrirSala>);
  return { mutate, reset };
}

function renderizar() {
  const { conexion, simular } = crearConexionFalsa();
  conexion.invocar = vi.fn(async () => {});
  render(
    <QueryClientProvider client={new QueryClient()}>
      <SesionProvider>
        <MemoryRouter initialEntries={[`/evaluaciones/${EVALUACION}/sala`]}>
          <Routes>
            <Route path='/evaluaciones/:evaluacionId/sala' element={<PaginaSalaDocente conexion={conexion} />} />
            <Route path='/evaluaciones/:evaluacionId' element={<p>DETALLE</p>} />
          </Routes>
        </MemoryRouter>
      </SesionProvider>
    </QueryClientProvider>,
  );
  return { conexion, simular };
}

// Retoma una sala guardada y espera a que el panel termine de unirse
async function renderizarPanel() {
  guardarSala(EVALUACION, docente.id, salaVigente());
  const entorno = renderizar();
  await waitFor(() => expect(entorno.conexion.invocar).toHaveBeenCalledWith('ObtenerEstadoSala'));
  return entorno;
}

beforeEach(() => {
  sessionStorage.clear();
  guardarToken(crearTokenFalso(docente));
  simularAbrir();
});

describe('PaginaSalaDocente', () => {
  it('sin sala guardada muestra el formulario para abrirla', () => {
    renderizar();
    expect(screen.getByRole('heading', { name: 'Sala en vivo' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Abrir sala' })).toBeInTheDocument();
  });

  it('pide abrir la sala con la evaluación de la URL y la capacidad por defecto', async () => {
    const { mutate } = simularAbrir();
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Abrir sala' }));

    expect(mutate).toHaveBeenCalledWith(
      { evaluacionId: EVALUACION, capacidadMaxima: 40 },
      expect.any(Object),
    );
  });

  it('al abrir la sala muestra el PIN y la guarda para retomarla', async () => {
    const { mutate } = simularAbrir();
    mutate.mockImplementation((_datos, opciones) => opciones.onSuccess(salaVigente()));
    renderizar();

    await userEvent.click(screen.getByRole('button', { name: 'Abrir sala' }));

    expect(await screen.findByLabelText('PIN de la sala: 654321')).toBeInTheDocument();
    expect(sessionStorage.getItem(`sala-docente:${EVALUACION}`)).not.toBeNull();
  });

  it('retoma la sala guardada sin volver a crearla', async () => {
    const { mutate } = simularAbrir();
    guardarSala(EVALUACION, docente.id, salaVigente());
    renderizar();

    expect(screen.getByLabelText('PIN de la sala: 654321')).toBeInTheDocument();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('descarta una sala guardada por otro usuario', () => {
    guardarSala(EVALUACION, 'otro-usuario', salaVigente());
    renderizar();
    expect(screen.getByRole('heading', { name: 'Abrir sala' })).toBeInTheDocument();
  });

  it('muestra el error del servidor al abrir la sala', () => {
    simularAbrir({ isError: true, error: new ApiError(403, { status: 403 }) });
    renderizar();
    expect(screen.getByText('Solo un docente o administrador puede abrir una sala.')).toBeInTheDocument();
  });

  it('al conectarse se une a la sala y pide el estado', async () => {
    const { conexion } = await renderizarPanel();
    expect(conexion.invocar).toHaveBeenCalledWith('UnirseASalaDocente', SALA_ID);
  });

  it('muestra a los alumnos que ingresan y el contador', async () => {
    const { simular } = await renderizarPanel();

    act(() =>
      simular('alumnoIngreso', {
        salaId: SALA_ID,
        alumnoId: 'aaaaaaaa-0000-4000-8000-000000000001',
        alias: 'Alumno 1',
        totalConectados: 1,
        ingresoUtc: new Date().toISOString(),
      }),
    );

    expect(screen.getByText('Alumno 1')).toBeInTheDocument();
    expect(screen.getByText('Ingresaron: 1')).toBeInTheDocument();
  });

  it('muestra la alerta de foco en texto, no solo en color', async () => {
    const { simular } = await renderizarPanel();
    const alumnoId = 'aaaaaaaa-0000-4000-8000-000000000001';

    act(() =>
      simular('alumnoIngreso', {
        salaId: SALA_ID,
        alumnoId,
        alias: 'Alumno 1',
        totalConectados: 1,
        ingresoUtc: new Date().toISOString(),
      }),
    );
    act(() =>
      simular('alertaFoco', {
        id: 'cccccccc-0000-4000-8000-000000000001',
        salaId: SALA_ID,
        alumnoId,
        preguntaId: null,
        tipoEvento: 'visibilitychange',
        duracionMs: 4500,
        severidad: 'Roja',
        registradoUtc: new Date().toISOString(),
      }),
    );

    expect(screen.getByText('Alerta roja (1)')).toBeInTheDocument();
  });

  it('Iniciar invoca IniciarSala y al llegar la pregunta muestra n/N', async () => {
    const { conexion, simular } = await renderizarPanel();

    const iniciar = screen.getByRole('button', { name: 'Iniciar' });
    await waitFor(() => expect(iniciar).toBeEnabled());
    await userEvent.click(iniciar);
    expect(conexion.invocar).toHaveBeenCalledWith('IniciarSala');

    act(() => {
      simular('salaIniciada', {
        salaId: SALA_ID,
        iniciadaUtc: new Date().toISOString(),
        totalPreguntas: 3,
        fechaLimiteUtc: enSegundos(1800),
      });
      simular('preguntaActual', pregunta(1));
    });

    expect(screen.getByText('Pregunta 1/3')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Siguiente pregunta (1/3)' })).toBeEnabled();
  });

  it('Siguiente pregunta invoca AvanzarPregunta', async () => {
    const { conexion, simular } = await renderizarPanel();
    act(() => {
      simular('salaIniciada', {
        salaId: SALA_ID,
        iniciadaUtc: new Date().toISOString(),
        totalPreguntas: 3,
        fechaLimiteUtc: enSegundos(1800),
      });
      simular('preguntaActual', pregunta(1));
    });

    const siguiente = screen.getByRole('button', { name: 'Siguiente pregunta (1/3)' });
    await waitFor(() => expect(siguiente).toBeEnabled());
    await userEvent.click(siguiente);

    expect(conexion.invocar).toHaveBeenCalledWith('AvanzarPregunta');
  });

  it('Finalizar pide confirmación y recién al confirmar invoca FinalizarSala', async () => {
    const { conexion } = await renderizarPanel();

    const finalizar = screen.getByRole('button', { name: 'Finalizar' });
    await waitFor(() => expect(finalizar).toBeEnabled());
    await userEvent.click(finalizar);
    expect(conexion.invocar).not.toHaveBeenCalledWith('FinalizarSala', 'Docente');

    await userEvent.click(screen.getByRole('button', { name: 'Sí, finalizar' }));
    expect(conexion.invocar).toHaveBeenCalledWith('FinalizarSala', 'Docente');
  });

  it('cancelar la confirmación no finaliza la sala', async () => {
    const { conexion } = await renderizarPanel();

    const finalizar = screen.getByRole('button', { name: 'Finalizar' });
    await waitFor(() => expect(finalizar).toBeEnabled());
    await userEvent.click(finalizar);
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));

    expect(conexion.invocar).not.toHaveBeenCalledWith('FinalizarSala', 'Docente');
  });

  it('muestra el cierre cuando llega salaFinalizada', async () => {
    const { simular } = await renderizarPanel();

    act(() =>
      simular('salaFinalizada', {
        salaId: SALA_ID,
        finalizadaUtc: new Date().toISOString(),
        motivo: 'Docente',
      }),
    );

    expect(screen.getByRole('heading', { name: 'Sala finalizada' })).toBeInTheDocument();
    expect(screen.getByText('Cerraste la sala.')).toBeInTheDocument();
  });

  it('«Abrir otra sala» con la sala finalizada vuelve al formulario sin pedir confirmación', async () => {
    const { simular } = await renderizarPanel();
    act(() =>
      simular('salaFinalizada', {
        salaId: SALA_ID,
        finalizadaUtc: new Date().toISOString(),
        motivo: 'Docente',
      }),
    );

    await userEvent.click(screen.getByRole('button', { name: 'Abrir otra sala' }));

    expect(screen.getByRole('heading', { name: 'Abrir sala' })).toBeInTheDocument();
    expect(sessionStorage.getItem(`sala-docente:${EVALUACION}`)).toBeNull();
  });

  it('«Abrir otra sala» con la sala vigente pide confirmación', async () => {
    await renderizarPanel();

    await userEvent.click(screen.getByRole('button', { name: 'Abrir otra sala' }));
    expect(screen.getByRole('heading', { name: '¿Abrir otra sala?' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Sí, abrir otra' }));
    expect(screen.getByRole('heading', { name: 'Abrir sala' })).toBeInTheDocument();
  });
});