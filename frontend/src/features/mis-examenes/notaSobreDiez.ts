import { formatearNota, formatearNumero } from '@/features/revision-notas/formato';

/** Nota llevada a escala 10, con un decimal como máximo: 4 de 5 → 8; 8,5 de 10 → 8,5; 7 de 9 → 7,8. null si no se puede calcular. */
export function notaSobreDiez(nota: number | null | undefined, maximo: number | null | undefined): number | null {
  if (nota == null || maximo == null || maximo <= 0) return null;
  return Math.round((nota / maximo) * 100) / 10;
}

/**
 * Cómo se muestra la nota al alumno. Si el examen ya es sobre 10 se muestra tal cual («8,5 / 10»).
 * Si no, la nota va primero en escala 10 y el puntaje original queda como detalle:
 * principal «8 / 10», detalle «4 / 5 puntos».
 */
export function textoNotaAlumno(nota: number | null | undefined, maximo: number | null | undefined) {
  const original = formatearNota(nota, maximo);
  if (original == null) return null;
  const sobreDiez = notaSobreDiez(nota, maximo);
  if (sobreDiez == null || maximo === 10) return { principal: original, detalle: null };
  return { principal: `${formatearNumero(sobreDiez)} / 10`, detalle: `${original} puntos` };
}
