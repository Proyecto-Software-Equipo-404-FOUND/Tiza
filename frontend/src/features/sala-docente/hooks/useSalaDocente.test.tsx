import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import type { ConexionSala } from '@/shared/realtime/tipos';
import type { AlertaFocoEvento, AlumnoIngresoEvento, PreguntaActual } from '../tipos';
import { useSalaDocente } from './useSalaDocente';

const SALA = '11111111-1111-4111-8111-111111111111';
const ALUMNO_1 = '22222222-2222-4222-8222-222222222221';
const ALUMNO_2 = '22222222-2222-4222-8222-222222222222';

const enSegundos = (s: number) => new Date(Date.now() + s * 1000).toISOString();

const pregunta = (cambios: Partial<PreguntaActual> = {}): PreguntaActual => ({
  salaId: SALA,
  preguntaId: '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60',
  indice: 1,
  totalPreguntas: 3,
  tipo: 'Abierta',
  enunciado: 'Explicá qué es una variable.',
  opciones: null,
  puntajeMaximo: 4,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(120),
  ...cambios,
});

const ingreso = (cambios: Partial<AlumnoIngresoEvento> = {}): AlumnoIngresoEvento => ({
  salaId: SALA,
  alumnoId: ALUMNO_1,
  alias: 'Alumno 1',
  totalConectados: 1,
  ingresoUtc: new Date().toISOString(),
  ...cambios,
});

const alerta = (cambios: Partial<AlertaFocoEvento> = {}): AlertaFocoEvento => ({
  id: 'a1',
  salaId: SALA,
  alumnoId: ALUMNO_1,
  preguntaId: null,
  tipoEvento: 'visibilitychange',
  duracionMs: 1000,
  severidad: 'Amarillo',
  registradoUtc: new Date().toISOString(),
  ...cambios,
});

type Manejo = (metodo: string, ...args: unknown[]) => Promise<unknown>;

function preparar(manejo: Manejo = async () => undefined) {
  const { conexion, simular, simularEstado } = crearConexionFalsa();
  const llamadas: Array<{ metodo: string; args: unknown[] }> = [];
  conexion.invocar = vi.fn(async (metodo: string, ...args: unknown[]) => {
    llamadas.push({ metodo, args });
    return manejo(metodo, ...args);
  }) as ConexionSala['invocar'];
  const vista = renderHook(() => useSalaDocente(conexion, SALA));
  const emitir = (evento: string, ...args: unknown[]) => act(() => simular(evento, ...args));
  const cambiarEstado = (estado: Parameters<typeof simularEstado>[0]) => act(() => simularEstado(estado));
  const metodos = () => llamadas.map((l) => l.metodo);
  return { vista, emitir, cambiarEstado, llamadas, metodos };
}

describe('useSalaDocente', () => {
  it('al conectar se une a la sala y después pide el estado', async () => {
    const { cambiarEstado, llamadas } = preparar();
    cambiarEstado('conectado');
    await waitFor(() => expect(llamadas).toHaveLength(2));
    expect(llamadas[0]).toEqual({ metodo: 'UnirseASalaDocente', args: [SALA] });
    expect(llamadas[1].metodo).toBe('ObtenerEstadoSala');
  });

  it('en cada reconexión vuelve a unirse, porque el hub pierde la sala', async () => {
    const { cambiarEstado, metodos } = preparar();
    cambiarEstado('conectado');
    await waitFor(() => expect(metodos()).toHaveLength(2));
    cambiarEstado('reconectando');
    cambiarEstado('conectado');
    await waitFor(() => expect(metodos()).toHaveLength(4));
    expect(metodos()).toEqual([
      'UnirseASalaDocente',
      'ObtenerEstadoSala',
      'UnirseASalaDocente',
      'ObtenerEstadoSala',
    ]);
  });

  it('si no puede unirse muestra el error del hub', async () => {
    const { vista, cambiarEstado } = preparar(async (metodo) => {
      if (metodo === 'UnirseASalaDocente') throw new Error('NO_AUTORIZADO: no pertenece a la institución');
      return undefined;
    });
    cambiarEstado('conectado');
    await waitFor(() => expect(vista.result.current.error).toBe('No tenés permiso para conducir esta sala.'));
  });

  it('los alumnos que ingresan se agregan sin duplicarse y el total lo manda el servidor', () => {
    const { vista, emitir } = preparar();
    emitir('alumnoIngreso', ingreso());
    emitir('alumnoIngreso', ingreso({ alumnoId: ALUMNO_2, alias: 'Alumno 2', totalConectados: 2 }));
    emitir('alumnoIngreso', ingreso({ totalConectados: 2 }));
    expect(vista.result.current.alumnos.map((a) => a.alias)).toEqual(['Alumno 1', 'Alumno 2']);
    expect(vista.result.current.totalConectados).toBe(2);
  });

  it('las alertas de foco suman, conservan la peor severidad y no se cuentan dos veces', () => {
    const { vista, emitir } = preparar();
    emitir('alumnoIngreso', ingreso());
    emitir('alertaFoco', alerta({ id: 'a1', severidad: 'Amarillo' }));
    emitir('alertaFoco', alerta({ id: 'a2', severidad: 'Roja' }));
    emitir('alertaFoco', alerta({ id: 'a2', severidad: 'Roja' }));
    emitir('alertaFoco', alerta({ id: 'a3', severidad: 'Amarillo' }));
    const [alumno] = vista.result.current.alumnos;
    expect(alumno.alertas).toBe(3);
    expect(alumno.peorSeveridad).toBe('roja');
  });

  it('una alerta de un alumno desconocido lo agrega con un alias de reemplazo', () => {
    const { vista, emitir } = preparar();
    emitir('alertaFoco', alerta({ alumnoId: ALUMNO_2 }));
    expect(vista.result.current.alumnos).toHaveLength(1);
    expect(vista.result.current.alumnos[0].alias).toBe('Alumno sin identificar');
  });

  it('estadoSala actualiza la fase, la pregunta, el límite y el desfase con el reloj del servidor', () => {
    const { vista, emitir } = preparar();
    const limite = enSegundos(1800);
    emitir('estadoSala', {
      salaId: SALA,
      estado: 'EnCurso',
      fechaLimiteUtc: limite,
      preguntaActual: pregunta({ indice: 2 }),
      servidorUtc: new Date(Date.now() + 5000).toISOString(),
    });
    const actual = vista.result.current;
    expect(actual.fase).toBe('enCurso');
    expect(actual.pregunta?.indice).toBe(2);
    expect(actual.fechaLimiteSala).toBe(limite);
    expect(actual.desfaseMs).toBeGreaterThan(4000);
    expect(actual.desfaseMs).toBeLessThan(6000);
  });

  it('salaIniciada y preguntaActual pasan la sala a «en curso»', () => {
    const { vista, emitir } = preparar();
    expect(vista.result.current.fase).toBe('espera');
    emitir('salaIniciada', { salaId: SALA, iniciadaUtc: new Date().toISOString(), totalPreguntas: 3, fechaLimiteUtc: enSegundos(1800) });
    emitir('preguntaActual', pregunta());
    expect(vista.result.current.fase).toBe('enCurso');
    expect(vista.result.current.pregunta?.indice).toBe(1);
  });

  it('solo se puede avanzar mientras no sea la última pregunta', () => {
    const { vista, emitir } = preparar();
    emitir('preguntaActual', pregunta({ indice: 2, totalPreguntas: 3 }));
    expect(vista.result.current.puedeAvanzar).toBe(true);
    emitir('preguntaActual', pregunta({ indice: 3, totalPreguntas: 3 }));
    expect(vista.result.current.puedeAvanzar).toBe(false);
  });

  it('salaFinalizada cierra la sala y guarda el motivo', () => {
    const { vista, emitir } = preparar();
    emitir('salaFinalizada', { salaId: SALA, finalizadaUtc: new Date().toISOString(), motivo: 'TiempoAgotado' });
    expect(vista.result.current.fase).toBe('finalizada');
    expect(vista.result.current.motivoCierre).toBe('TiempoAgotado');
  });

  it('Iniciar, Siguiente y Finalizar invocan el método del hub', async () => {
    const { vista, llamadas } = preparar();
    await act(() => vista.result.current.iniciar());
    await act(() => vista.result.current.siguiente());
    await act(() => vista.result.current.finalizar());
    expect(llamadas).toEqual([
      { metodo: 'IniciarSala', args: [] },
      { metodo: 'AvanzarPregunta', args: [null] },
      { metodo: 'FinalizarSala', args: ['Docente'] },
    ]);
    expect(vista.result.current.trabajando).toBe(false);
  });

  it('si la acción falla muestra el mensaje del código', async () => {
    const { vista } = preparar(async () => {
      throw new Error('SIN_PREGUNTAS: La evaluación no tiene preguntas.');
    });
    await act(() => vista.result.current.iniciar());
    expect(vista.result.current.error).toBe('La evaluación no tiene preguntas para tomar.');
  });

  it('si la sala ya estaba iniciada avisa y vuelve a pedir el estado', async () => {
    const { vista, metodos } = preparar(async (metodo) => {
      if (metodo === 'IniciarSala') throw new Error('SALA_YA_INICIADA: ya iniciada');
      return undefined;
    });
    await act(() => vista.result.current.iniciar());
    expect(vista.result.current.error).toBe('La sala ya estaba iniciada.');
    expect(metodos()).toEqual(['IniciarSala', 'ObtenerEstadoSala']);
  });

  it('al desmontar ignora los eventos que lleguen después', () => {
    const { vista, emitir } = preparar();
    vista.unmount();
    expect(() => emitir('alumnoIngreso', ingreso())).not.toThrow();
  });

  it('cuenta quiénes respondieron la pregunta actual y vuelve a cero al avanzar', () => {
    const { vista, emitir } = preparar();
    emitir('preguntaActual', pregunta({ preguntaId: 'p1', indice: 1 }));
    emitir('respuestaRegistrada', { salaId: SALA, alumnoId: 'a1', preguntaId: 'p1', recibidaUtc: 'x' });
    emitir('respuestaRegistrada', { salaId: SALA, alumnoId: 'a1', preguntaId: 'p1', recibidaUtc: 'x' });
    emitir('respuestaRegistrada', { salaId: SALA, alumnoId: 'a2', preguntaId: 'p1', recibidaUtc: 'x' });
    expect(vista.result.current.respondieronPreguntaActual).toBe(2);

    emitir('preguntaActual', pregunta({ preguntaId: 'p2', indice: 2 }));
    expect(vista.result.current.respondieronPreguntaActual).toBe(0);
  });

  it('toma el total de conectados de la foto de presencia', () => {
    const { vista, emitir } = preparar();
    emitir('alumnosConectados', { salaId: SALA, totalConectados: 3, alumnos: [], actualizadoUtc: 'x' });
    expect(vista.result.current.totalConectados).toBe(3);
  });
});
