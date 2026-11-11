import type { SalaCreada } from './tipos';

const prefijo = (evaluacionId: string) => `sala-docente:${evaluacionId}`;

type Guardado = { usuarioId: string; sala: SalaCreada };

function esSalaCreada(valor: unknown): valor is SalaCreada {
  if (typeof valor !== 'object' || valor === null) return false;
  const v = valor as Record<string, unknown>;
  return (
    typeof v.id === 'string' &&
    typeof v.codigoPIN === 'number' &&
    typeof v.fechaVencimiento === 'string' &&
    typeof v.capacidadMaxima === 'number'
  );
}

// Guarda la sala abierta para poder retomarla si se recarga la pestaña (no hay GET de la sala)
export function guardarSala(evaluacionId: string, usuarioId: string, sala: SalaCreada): void {
  try {
    const dato: Guardado = { usuarioId, sala };
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

// Devuelve la sala guardada si sigue siendo de este usuario y no venció; si no, la descarta
export function recuperarSala(evaluacionId: string, usuarioId: string, ahora: number = Date.now()): SalaCreada | null {
  try {
    const crudo = sessionStorage.getItem(prefijo(evaluacionId));
    if (!crudo) return null;
    const dato = JSON.parse(crudo) as Partial<Guardado>;
    const vencimiento = esSalaCreada(dato.sala) ? Date.parse(dato.sala.fechaVencimiento) : NaN;
    if (dato.usuarioId !== usuarioId || !esSalaCreada(dato.sala) || Number.isNaN(vencimiento) || vencimiento <= ahora) {
      olvidarSala(evaluacionId);
      return null;
    }
    return dato.sala;
  } catch {
    olvidarSala(evaluacionId);
    return null;
  }
}