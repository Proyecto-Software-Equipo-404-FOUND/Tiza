import type { Alumno, Matricula } from './api/matriculas';

/**
 * Nombre para mostrar de un matriculado. Cursos solo devuelve el id del alumno, así que se busca
 * en la lista de Identidad; si todavía no hay forma de saberlo, se muestra una referencia corta
 * (no el id entero) para poder distinguir a unos alumnos de otros.
 */
export function nombreDeAlumno(matricula: Matricula, alumnos: Alumno[]): string {
  const conocido = alumnos.find((a) => a.id === matricula.alumnoId)?.nombre;
  if (conocido) return conocido;
  if (matricula.alumnoNombre) return matricula.alumnoNombre;
  return `Alumno …${matricula.alumnoId.slice(-6)}`;
}

/** El contrato define `estado` como texto; hoy: Activa, Baja o Completada. Solo «Baja» va a inactivas. */
export const estaDeBaja = (matricula: Matricula): boolean => matricula.estado === 'Baja';

/** «31/10/2026», o null si la fecha falta o no es válida. */
export function fechaCorta(fechaIso?: string): string | null {
  if (!fechaIso) return null;
  const fecha = new Date(fechaIso);
  return Number.isNaN(fecha.getTime()) ? null : fecha.toLocaleDateString('es-AR', { timeZone: 'UTC' });
}
