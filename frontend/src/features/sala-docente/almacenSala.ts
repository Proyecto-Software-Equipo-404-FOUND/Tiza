import type { SalaCreada } from './tipos';

const prefijo = (evaluacionId: string) => `sala-docente:${evaluacionId}`;

// Plazo máximo para iniciar una sala creada que todavía no empezó (S3-SA-02, #239)
const ESPERA_MAXIMA_SIN_INICIAR_MS = 24 * 60 * 60 * 1000;

type Guardado = { usuarioId: string; sala: SalaCreada; guardadaEn?: number };

function esSalaCreada(valor: unknown): valor is SalaCreada {
  if (typeof valor !== 'object' || valor === null) return false;
  const v = valor as Record<string, unknown>;
  return (
    typeof v.id === 'string' &&
    typeof v.codigoPIN === 'number' &&
    // null hasta que el docente inicia la sala
    (typeof v.fechaVencimiento === 'string' || v.fechaVencimiento === null) &&
    typeof v.capacidadMaxima === 'number'
  );
}

// Guarda la sala abierta para poder retomarla si se recarga la pestaña (no hay GET de la sala)
export function guardarSala(evaluacionId: string, usuarioId: string, sala: SalaCreada, ahora: number = Date.now()): void {
  try {
    const dato: Guardado = { usuarioId, sala, guardadaEn: ahora };
    sessionStorage.setItem(prefijo(evaluacionId), JSON.stringify(dato));
  } catch {
    // sin almacenamiento: la sala sigue andando, solo que no se podrá retomar al recargar
  }
}

export function olvidarSala(evaluacionId: string): void {
  try {
    sessionStorage.removeItem(prefijo(evaluacionId));
  } catch {
    // nada que hacer
  }
}

// Con fechaVencimiento vale esa fecha. Sin ella (la sala todavía no se inició) rige el plazo máximo para
// iniciarla, contado desde que se guardó. null = no se puede saber: la sala se conserva.
function vencimientoDe(sala: SalaCreada, guardadaEn: number | undefined): number | null {
  if (sala.fechaVencimiento !== null) return Date.parse(sala.fechaVencimiento);
  return guardadaEn === undefined ? null : guardadaEn + ESPERA_MAXIMA_SIN_INICIAR_MS;
}

// Devuelve la sala guardada si sigue siendo de este usuario y no venció; si no, la descarta
export function recuperarSala(evaluacionId: string, usuarioId: string, ahora: number = Date.now()): SalaCreada | null {
  try {
    const crudo = sessionStorage.getItem(prefijo(evaluacionId));
    if (!crudo) return null;
    const dato = JSON.parse(crudo) as Partial<Guardado>;
    if (dato.usuarioId !== usuarioId || !esSalaCreada(dato.sala)) {
      olvidarSala(evaluacionId);
      return null;
    }
    const vencimiento = vencimientoDe(dato.sala, dato.guardadaEn);
    if (vencimiento !== null && (Number.isNaN(vencimiento) || vencimiento <= ahora)) {
      olvidarSala(evaluacionId);
      return null;
    }
    return dato.sala;
  } catch {
    olvidarSala(evaluacionId);
    return null;
  }
}