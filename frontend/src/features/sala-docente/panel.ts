import type { AlertaFocoEvento, AlumnoIngresoEvento, AlumnoPanel, PreguntaActual, SeveridadFoco } from './tipos';

export const ALIAS_SIN_IDENTIFICAR = 'Alumno sin identificar';

const GRAVEDAD: Record<SeveridadFoco, number> = { ninguna: 0, amarilla: 1, roja: 2 };

// El backend manda «Amarillo» y «Roja» (género distinto): se compara sin mayúsculas y se toleran variantes.
// Cualquier otro valor queda en «ninguna» para no romper la pantalla.
export function normalizarSeveridad(valor: unknown): SeveridadFoco {
  if (typeof valor !== 'string') return 'ninguna';
  const texto = valor.trim().toLowerCase();
  if (texto === 'roja' || texto === 'rojo') return 'roja';
  if (texto === 'amarillo' || texto === 'amarilla') return 'amarilla';
  return 'ninguna';
}

export function peorSeveridad(a: SeveridadFoco, b: SeveridadFoco): SeveridadFoco {
  return GRAVEDAD[a] >= GRAVEDAD[b] ? a : b;
}

// Un alumno puede disparar `alumnoIngreso` más de una vez (se une y se reconecta): no se duplica
export function registrarIngreso(alumnos: AlumnoPanel[], evento: AlumnoIngresoEvento): AlumnoPanel[] {
  const existe = alumnos.some((alumno) => alumno.alumnoId === evento.alumnoId);
  if (!existe) {
    return [
      ...alumnos,
      {
        alumnoId: evento.alumnoId,
        alias: evento.alias,
        ingresoUtc: evento.ingresoUtc,
        alertas: 0,
        peorSeveridad: 'ninguna',
      },
    ];
  }
  return alumnos.map((alumno) =>
    alumno.alumnoId === evento.alumnoId
      ? { ...alumno, alias: evento.alias, ingresoUtc: alumno.ingresoUtc ?? evento.ingresoUtc }
      : alumno,
  );
}

export type ResultadoAlerta = { alumnos: AlumnoPanel[]; idsVistos: ReadonlySet<string> };

// Suma una alerta de foco. Se deduplica por `id` y se conserva la peor severidad vista.
// Si el alumno no está en la lista (entró antes que el docente) se agrega con un alias de reemplazo.
export function registrarAlerta(
  alumnos: AlumnoPanel[],
  alerta: AlertaFocoEvento,
  idsVistos: ReadonlySet<string>,
): ResultadoAlerta {
  if (idsVistos.has(alerta.id)) return { alumnos, idsVistos };

  const severidad = normalizarSeveridad(alerta.severidad);
  const nuevosIds = new Set(idsVistos).add(alerta.id);
  const existe = alumnos.some((alumno) => alumno.alumnoId === alerta.alumnoId);

  if (!existe) {
    return {
      alumnos: [
        ...alumnos,
        {
          alumnoId: alerta.alumnoId,
          alias: ALIAS_SIN_IDENTIFICAR,
          ingresoUtc: null,
          alertas: 1,
          peorSeveridad: severidad,
        },
      ],
      idsVistos: nuevosIds,
    };
  }

  return {
    alumnos: alumnos.map((alumno) =>
      alumno.alumnoId === alerta.alumnoId
        ? {
            ...alumno,
            alertas: alumno.alertas + 1,
            peorSeveridad: peorSeveridad(alumno.peorSeveridad, severidad),
          }
        : alumno,
    ),
    idsVistos: nuevosIds,
  };
}

// «2/5» para el botón de siguiente pregunta, o null si todavía no hay pregunta vigente
export function etiquetaPregunta(pregunta: Pick<PreguntaActual, 'indice' | 'totalPreguntas'> | null | undefined): string | null {
  if (!pregunta || pregunta.totalPreguntas <= 0) return null;
  return `${pregunta.indice}/${pregunta.totalPreguntas}`;
}

// Diferencia entre el reloj del servidor y el del navegador (servidor − navegador)
export function calcularDesfaseMs(servidorUtc: string | undefined, ahora: number = Date.now()): number {
  if (!servidorUtc) return 0;
  const servidor = Date.parse(servidorUtc);
  return Number.isNaN(servidor) ? 0 : servidor - ahora;
}

// Segundos que faltan para `fechaLimiteUtc` según el reloj del servidor; null si no hay fecha válida
export function segundosRestantes(
  fechaLimiteUtc: string | null | undefined,
  desfaseMs: number,
  ahora: number = Date.now(),
): number | null {
  if (!fechaLimiteUtc) return null;
  const limite = Date.parse(fechaLimiteUtc);
  if (Number.isNaN(limite)) return null;
  return Math.max(0, Math.ceil((limite - (ahora + desfaseMs)) / 1000));
}

export function formatearTiempo(segundos: number): string {
  const minutos = Math.floor(segundos / 60);
  const resto = segundos % 60;
  return `${String(minutos).padStart(2, '0')}:${String(resto).padStart(2, '0')}`;
}