import { describe, expect, it } from 'vitest';
import {
  ALIAS_SIN_IDENTIFICAR,
  aplicarFotoDePresencia,
  calcularDesfaseMs,
  etiquetaPregunta,
  formatearTiempo,
  normalizarSeveridad,
  registrarAlerta,
  registrarIngreso,
  registrarSalida,
  segundosRestantes,
} from './panel';
import type { AlertaFocoEvento, AlumnoIngresoEvento, AlumnoPanel, AlumnoSalioEvento, AlumnosConectadosEvento } from './tipos';

const SALA = '11111111-1111-4111-8111-111111111111';
const ALUMNO_1 = '22222222-2222-4222-8222-222222222221';
const ALUMNO_2 = '22222222-2222-4222-8222-222222222222';

const ingreso = (cambios: Partial<AlumnoIngresoEvento> = {}): AlumnoIngresoEvento => ({
  salaId: SALA,
  alumnoId: ALUMNO_1,
  alias: 'Alumno 1',
  totalConectados: 1,
  ingresoUtc: '2026-11-05T14:00:00.000Z',
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
  registradoUtc: '2026-11-05T14:01:00.000Z',
  ...cambios,
});

const alumno = (cambios: Partial<AlumnoPanel> = {}): AlumnoPanel => ({
  alumnoId: ALUMNO_1,
  alias: 'Alumno 1',
  ingresoUtc: '2026-11-05T14:00:00.000Z',
  alertas: 0,
  peorSeveridad: 'ninguna',
  ...cambios,
});

describe('normalizarSeveridad', () => {
  it('reconoce los valores del backend y sus variantes sin importar mayúsculas', () => {
    expect(normalizarSeveridad('Roja')).toBe('roja');
    expect(normalizarSeveridad('rojo')).toBe('roja');
    expect(normalizarSeveridad('Amarillo')).toBe('amarilla');
    expect(normalizarSeveridad(' AMARILLA ')).toBe('amarilla');
  });

  it('un valor desconocido o que no es texto queda en «ninguna»', () => {
    expect(normalizarSeveridad('Naranja')).toBe('ninguna');
    expect(normalizarSeveridad('')).toBe('ninguna');
    expect(normalizarSeveridad(null)).toBe('ninguna');
    expect(normalizarSeveridad(3)).toBe('ninguna');
  });
});

describe('registrarIngreso', () => {
  it('agrega al alumno que entra', () => {
    expect(registrarIngreso([], ingreso())).toEqual([alumno()]);
  });

  it('si el alumno se reconecta no se duplica y conserva sus alertas', () => {
    const lista = [alumno({ alertas: 2, peorSeveridad: 'roja' })];
    const resultado = registrarIngreso(lista, ingreso({ ingresoUtc: '2026-11-05T14:05:00.000Z' }));
    expect(resultado).toHaveLength(1);
    expect(resultado[0].alertas).toBe(2);
    expect(resultado[0].peorSeveridad).toBe('roja');
    expect(resultado[0].ingresoUtc).toBe('2026-11-05T14:00:00.000Z');
  });

  it('completa el alias de un alumno que había llegado solo por una alerta', () => {
    const lista = [alumno({ alias: ALIAS_SIN_IDENTIFICAR, ingresoUtc: null, alertas: 1 })];
    const [resultado] = registrarIngreso(lista, ingreso());
    expect(resultado.alias).toBe('Alumno 1');
    expect(resultado.ingresoUtc).toBe('2026-11-05T14:00:00.000Z');
    expect(resultado.alertas).toBe(1);
  });

  it('no modifica la lista original', () => {
    const lista = [alumno()];
    registrarIngreso(lista, ingreso({ alias: 'Otro' }));
    expect(lista[0].alias).toBe('Alumno 1');
  });

  it('agrega al invitado con el nombre que escribió y conserva su alias', () => {
    const resultado = registrarIngreso([], ingreso({ alias: 'Alumno 7', esInvitado: true, nombre: 'Lucía Fernández' }));
    expect(resultado).toEqual([alumno({ alias: 'Alumno 7', esInvitado: true, nombre: 'Lucía Fernández' })]);
  });

  it('un alumno con cuenta no queda marcado como invitado ni trae nombre', () => {
    const [resultado] = registrarIngreso([], ingreso({ esInvitado: false }));
    expect(resultado).not.toHaveProperty('esInvitado');
    expect(resultado).not.toHaveProperty('nombre');
  });

  it('completa el nombre de un invitado que había llegado solo por una alerta', () => {
    const lista = [alumno({ alias: ALIAS_SIN_IDENTIFICAR, ingresoUtc: null, alertas: 1 })];
    const [resultado] = registrarIngreso(lista, ingreso({ alias: 'Alumno 7', esInvitado: true, nombre: 'Lucía Fernández' }));
    expect(resultado).toMatchObject({ alias: 'Alumno 7', esInvitado: true, nombre: 'Lucía Fernández', alertas: 1 });
  });
});

describe('registrarAlerta', () => {
  it('suma la alerta al alumno y guarda su severidad', () => {
    const { alumnos, idsVistos } = registrarAlerta([alumno()], alerta(), new Set());
    expect(alumnos[0].alertas).toBe(1);
    expect(alumnos[0].peorSeveridad).toBe('amarilla');
    expect(idsVistos.has('a1')).toBe(true);
  });

  it('una roja pisa a una amarilla, pero una amarilla no baja una roja', () => {
    let estado = registrarAlerta([alumno()], alerta({ id: 'a1', severidad: 'Amarillo' }), new Set());
    estado = registrarAlerta(estado.alumnos, alerta({ id: 'a2', severidad: 'Roja' }), estado.idsVistos);
    expect(estado.alumnos[0].peorSeveridad).toBe('roja');
    estado = registrarAlerta(estado.alumnos, alerta({ id: 'a3', severidad: 'Amarillo' }), estado.idsVistos);
    expect(estado.alumnos[0].peorSeveridad).toBe('roja');
    expect(estado.alumnos[0].alertas).toBe(3);
  });

  it('ignora una alerta con un id que ya vio', () => {
    const primera = registrarAlerta([alumno()], alerta(), new Set());
    const repetida = registrarAlerta(primera.alumnos, alerta(), primera.idsVistos);
    expect(repetida.alumnos[0].alertas).toBe(1);
    expect(repetida.alumnos).toBe(primera.alumnos);
  });

  it('si el alumno no estaba en la lista lo agrega con un alias de reemplazo', () => {
    const { alumnos } = registrarAlerta([alumno()], alerta({ alumnoId: ALUMNO_2, severidad: 'Roja' }), new Set());
    expect(alumnos).toHaveLength(2);
    expect(alumnos[1]).toEqual({
      alumnoId: ALUMNO_2,
      alias: ALIAS_SIN_IDENTIFICAR,
      ingresoUtc: null,
      alertas: 1,
      peorSeveridad: 'roja',
    });
  });

  it('una severidad desconocida cuenta como alerta pero no cambia el color', () => {
    const { alumnos } = registrarAlerta([alumno()], alerta({ severidad: 'Naranja' }), new Set());
    expect(alumnos[0].alertas).toBe(1);
    expect(alumnos[0].peorSeveridad).toBe('ninguna');
  });

  it('no modifica la lista ni el conjunto originales', () => {
    const lista = [alumno()];
    const ids = new Set<string>();
    registrarAlerta(lista, alerta(), ids);
    expect(lista[0].alertas).toBe(0);
    expect(ids.size).toBe(0);
  });
});

describe('etiquetaPregunta', () => {
  it('devuelve «n/N» cuando hay pregunta vigente', () => {
    expect(etiquetaPregunta({ indice: 2, totalPreguntas: 5 })).toBe('2/5');
  });

  it('devuelve null si todavía no hay pregunta o el total no es válido', () => {
    expect(etiquetaPregunta(null)).toBeNull();
    expect(etiquetaPregunta(undefined)).toBeNull();
    expect(etiquetaPregunta({ indice: 1, totalPreguntas: 0 })).toBeNull();
  });
});

describe('cronómetro con el reloj del servidor', () => {
  const ahora = Date.parse('2026-11-05T14:00:00.000Z');
  const en = (ms: number) => new Date(ahora + ms).toISOString();

  it('calcula la diferencia entre el reloj del servidor y el del navegador', () => {
    expect(calcularDesfaseMs(en(10_000), ahora)).toBe(10_000);
    expect(calcularDesfaseMs(en(-4_000), ahora)).toBe(-4_000);
    expect(calcularDesfaseMs(undefined, ahora)).toBe(0);
    expect(calcularDesfaseMs('no-es-fecha', ahora)).toBe(0);
  });

  it('sin desfase cuenta los segundos hasta el límite', () => {
    expect(segundosRestantes(en(90_000), 0, ahora)).toBe(90);
  });

  it('si el servidor está adelantado, falta menos tiempo', () => {
    expect(segundosRestantes(en(90_000), 10_000, ahora)).toBe(80);
  });

  it('redondea hacia arriba y nunca baja de cero', () => {
    expect(segundosRestantes(en(1_500), 0, ahora)).toBe(2);
    expect(segundosRestantes(en(-5_000), 0, ahora)).toBe(0);
  });

  it('devuelve null si no hay fecha límite válida', () => {
    expect(segundosRestantes(null, 0, ahora)).toBeNull();
    expect(segundosRestantes('no-es-fecha', 0, ahora)).toBeNull();
  });

  it('formatea como mm:ss', () => {
    expect(formatearTiempo(0)).toBe('00:00');
    expect(formatearTiempo(5)).toBe('00:05');
    expect(formatearTiempo(90)).toBe('01:30');
    expect(formatearTiempo(3600)).toBe('60:00');
  });
});

describe('registrarSalida', () => {
  const salida = (cambios: Partial<AlumnoSalioEvento> = {}): AlumnoSalioEvento => ({
    salaId: SALA,
    alumnoId: ALUMNO_1,
    alias: 'Alumno 1',
    totalConectados: 0,
    salidaUtc: '2026-11-05T14:30:00.000Z',
    ...cambios,
  });

  it('marca como desconectado al alumno y conserva sus alertas', () => {
    const lista = [alumno({ alertas: 2, peorSeveridad: 'roja' })];
    const resultado = registrarSalida(lista, salida());
    expect(resultado[0]).toMatchObject({ desconectado: true, alertas: 2, peorSeveridad: 'roja' });
  });

  it('no agrega a un alumno que nunca estuvo en la lista', () => {
    expect(registrarSalida([], salida())).toEqual([]);
  });

  it('solo toca al alumno que salió', () => {
    const lista = [alumno(), alumno({ alumnoId: ALUMNO_2, alias: 'Alumno 2' })];
    const resultado = registrarSalida(lista, salida());
    expect(resultado[0].desconectado).toBe(true);
    expect(resultado[1].desconectado).toBeUndefined();
  });

  it('al reconectarse con registrarIngreso deja de figurar como desconectado', () => {
    const lista = registrarSalida([alumno()], salida());
    const resultado = registrarIngreso(lista, ingreso());
    expect(resultado[0].desconectado).toBeUndefined();
  });
});

describe('aplicarFotoDePresencia', () => {
  const foto = (alumnos: AlumnosConectadosEvento['alumnos']): AlumnosConectadosEvento => ({
    salaId: SALA,
    totalConectados: alumnos.length,
    alumnos,
    actualizadoUtc: '2026-11-05T14:30:00.000Z',
  });

  it('con la lista vacía (recarga del docente) agrega a todos los conectados', () => {
    const resultado = aplicarFotoDePresencia([], foto([{ alumnoId: ALUMNO_1, alias: 'Alumno 1', ingresoUtc: 'x' }]));
    expect(resultado).toEqual([
      { alumnoId: ALUMNO_1, alias: 'Alumno 1', ingresoUtc: 'x', alertas: 0, peorSeveridad: 'ninguna' },
    ]);
  });

  it('conserva las alertas, corrige el alias y reconecta al que figuraba como desconectado', () => {
    const lista = [alumno({ alias: 'Alumno sin identificar', alertas: 2, peorSeveridad: 'roja', desconectado: true })];
    const resultado = aplicarFotoDePresencia(lista, foto([{ alumnoId: ALUMNO_1, alias: 'Alumno 1', ingresoUtc: 'x' }]));
    expect(resultado[0]).toMatchObject({ alias: 'Alumno 1', alertas: 2, peorSeveridad: 'roja' });
    expect(resultado[0].desconectado).toBeUndefined();
  });

  it('marca como desconectado al que ya no figura, sin sacarlo de la lista', () => {
    const lista = [alumno({ alertas: 1 })];
    const resultado = aplicarFotoDePresencia(lista, foto([]));
    expect(resultado).toHaveLength(1);
    expect(resultado[0]).toMatchObject({ desconectado: true, alertas: 1 });
  });

  it('con la lista vacía agrega al invitado con su nombre', () => {
    const resultado = aplicarFotoDePresencia(
      [],
      foto([{ alumnoId: ALUMNO_1, alias: 'Alumno 7', esInvitado: true, nombre: 'Lucía Fernández', ingresoUtc: 'x' }]),
    );
    expect(resultado).toEqual([
      {
        alumnoId: ALUMNO_1,
        alias: 'Alumno 7',
        esInvitado: true,
        nombre: 'Lucía Fernández',
        ingresoUtc: 'x',
        alertas: 0,
        peorSeveridad: 'ninguna',
      },
    ]);
  });

  it('completa el nombre de un invitado que ya figuraba y conserva sus alertas', () => {
    const lista = [alumno({ alias: ALIAS_SIN_IDENTIFICAR, alertas: 2, peorSeveridad: 'roja' })];
    const resultado = aplicarFotoDePresencia(
      lista,
      foto([{ alumnoId: ALUMNO_1, alias: 'Alumno 7', esInvitado: true, nombre: 'Lucía Fernández', ingresoUtc: 'x' }]),
    );
    expect(resultado[0]).toMatchObject({
      alias: 'Alumno 7',
      esInvitado: true,
      nombre: 'Lucía Fernández',
      alertas: 2,
      peorSeveridad: 'roja',
    });
  });
});