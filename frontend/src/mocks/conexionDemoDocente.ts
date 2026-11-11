import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import type { ConexionSala } from '@/shared/realtime/tipos';
import type { EstadoDeSala, PreguntaActual } from '@/features/sala-docente/tipos';

const TOTAL_PREGUNTAS = 3;
const SEGUNDOS_POR_PREGUNTA = 90;
const SEGUNDOS_DE_SALA = 1800;

const ALUMNOS = [1, 2, 3, 4].map((n) => ({
  alumnoId: `aaaaaaaa-0000-4000-8000-00000000000${n}`,
  alias: `Alumno ${n}`,
}));

const esperar = (ms: number) => new Promise((resolver) => setTimeout(resolver, ms));
const enSegundos = (segundos: number) => new Date(Date.now() + segundos * 1000).toISOString();

const preguntaNumero = (salaId: string, indice: number): PreguntaActual => ({
  salaId,
  preguntaId: `bbbbbbbb-0000-4000-8000-00000000000${indice}`,
  indice,
  totalPreguntas: TOTAL_PREGUNTAS,
  tipo: 'Abierta',
  enunciado: `Pregunta de prueba ${indice}`,
  opciones: null,
  puntajeMaximo: 2,
  fechaInicioUtc: new Date().toISOString(),
  fechaLimiteUtc: enSegundos(SEGUNDOS_POR_PREGUNTA),
});

// Sala de mentira para la demo del panel del docente.
// Al unirse el docente, «entran» 4 alumnos de a uno (el 2 entra dos veces, como una reconexión).
// Al iniciar, llegan respuestas y alertas de foco simuladas (una amarilla y una roja).
// Errores del hub como el real: «CODIGO: mensaje».
export function crearConexionDemoDocente(): ConexionSala {
  const { conexion, simular } = crearConexionFalsa();

  let salaId = '';
  let estado: EstadoDeSala = 'Activa';
  let indice = 0;
  let fechaLimiteSala: string | null = null;
  let alumnosSimulados = false;
  const ingresaron = new Set<string>();

  const sigueEn = (esperado: EstadoDeSala) => estado === esperado;

  const ingresar = (posicion: number) => {
    const alumno = ALUMNOS[posicion];
    ingresaron.add(alumno.alumnoId);
    simular('alumnoIngreso', {
      salaId,
      alumnoId: alumno.alumnoId,
      alias: alumno.alias,
      totalConectados: ingresaron.size,
      ingresoUtc: new Date().toISOString(),
    });
  };

  const simularAlumnos = () => {
    if (alumnosSimulados) return;
    alumnosSimulados = true;
    [0, 1, 2, 3].forEach((posicion, i) => setTimeout(() => sigueEn('Activa') && ingresar(posicion), 1500 * (i + 1)));
    setTimeout(() => sigueEn('Activa') && ingresar(1), 8000); // reconexión: mismo alumno, no suma
  };

  const simularActividad = () => {
    const alerta = (posicion: number, severidad: string, duracionMs: number, id: string) =>
      simular('alertaFoco', {
        id,
        salaId,
        alumnoId: ALUMNOS[posicion].alumnoId,
        preguntaId: null,
        tipoEvento: 'visibilitychange',
        duracionMs,
        severidad,
        registradoUtc: new Date().toISOString(),
      });
    const respuesta = (posicion: number) =>
      simular('respuestaRegistrada', {
        salaId,
        alumnoId: ALUMNOS[posicion].alumnoId,
        preguntaId: `bbbbbbbb-0000-4000-8000-00000000000${Math.max(indice, 1)}`,
        recibidaUtc: new Date().toISOString(),
      });

    setTimeout(() => sigueEn('EnCurso') && respuesta(0), 3000);
    setTimeout(() => sigueEn('EnCurso') && alerta(1, 'Amarillo', 1200, 'cccccccc-0000-4000-8000-000000000001'), 5000);
    setTimeout(() => sigueEn('EnCurso') && alerta(2, 'Roja', 4500, 'cccccccc-0000-4000-8000-000000000002'), 9000);
    setTimeout(() => sigueEn('EnCurso') && respuesta(1), 11000);
  };

  const emitirEstado = () =>
    simular('estadoSala', {
      salaId,
      estado,
      fechaLimiteUtc: fechaLimiteSala,
      preguntaActual: indice > 0 ? preguntaNumero(salaId, indice) : null,
      ultimaSecuencia: 0,
      preguntasRespondidas: [],
      servidorUtc: new Date().toISOString(),
    });

  const sinIniciar = () => new Error('SALA_NO_INICIADA: la sala todavía no fue iniciada.');
  const finalizada = () => new Error('SALA_FINALIZADA: la sala ya cerró.');

  return {
    ...conexion,
    invocar: async (metodo, ...argumentos) => {
      await esperar(300);

      if (metodo === 'UnirseASalaDocente') {
        salaId = String(argumentos[0]);
        simularAlumnos();
        return undefined;
      }

      if (!salaId) throw new Error('SALA_NO_ENCONTRADA: el docente todavía no se unió a una sala.');

      if (metodo === 'ObtenerEstadoSala') {
        emitirEstado();
        return undefined;
      }

      if (metodo === 'IniciarSala') {
        if (sigueEn('Finalizada')) throw finalizada();
        if (sigueEn('EnCurso')) return undefined; // idempotente
        estado = 'EnCurso';
        indice = 1;
        fechaLimiteSala = enSegundos(SEGUNDOS_DE_SALA);
        simular('salaIniciada', {
          salaId,
          iniciadaUtc: new Date().toISOString(),
          totalPreguntas: TOTAL_PREGUNTAS,
          fechaLimiteUtc: fechaLimiteSala,
        });
        simular('preguntaActual', preguntaNumero(salaId, indice));
        simularActividad();
        return undefined;
      }

      if (metodo === 'AvanzarPregunta') {
        if (sigueEn('Finalizada')) throw finalizada();
        if (sigueEn('Activa')) throw sinIniciar();
        if (indice >= TOTAL_PREGUNTAS) throw new Error('PREGUNTA_INEXISTENTE: no hay más preguntas.');
        indice += 1;
        simular('preguntaActual', preguntaNumero(salaId, indice));
        return undefined;
      }

      if (metodo === 'FinalizarSala') {
        if (sigueEn('Finalizada')) return undefined; // idempotente
        estado = 'Finalizada';
        simular('salaFinalizada', {
          salaId,
          finalizadaUtc: new Date().toISOString(),
          motivo: 'Docente',
        });
        return undefined;
      }

      return undefined;
    },
  };
}