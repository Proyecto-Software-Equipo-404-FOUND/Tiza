import type { AlertaFocoDetalle, EstadoEntrega, UsuarioResumen } from './tipos';

const NUMERO = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 });

/** 3.5 → «3,5». */
export const formatearNumero = (valor: number) => NUMERO.format(valor);

/** «3,5 / 4». Con el máximo en null o sin nota devuelve null: quien llama decide qué mostrar. */
export function formatearNota(nota: number | null | undefined, maximo: number | null | undefined) {
  if (nota == null) return null;
  return maximo == null ? formatearNumero(nota) : `${formatearNumero(nota)} / ${formatearNumero(maximo)}`;
}

/** 800 → «menos de 1 s»; 4200 → «4 s»; 65000 → «1 min 5 s». */
export function formatearDuracion(ms: number) {
  if (ms < 1000) return 'menos de 1 s';
  const segundos = Math.round(ms / 1000);
  if (segundos < 60) return `${segundos} s`;
  const minutos = Math.floor(segundos / 60);
  const resto = segundos % 60;
  return resto === 0 ? `${minutos} min` : `${minutos} min ${resto} s`;
}

export const formatearFechaHora = (iso: string) =>
  new Date(iso).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' });

export const etiquetaEstadoEntrega: Record<EstadoEntrega, string> = {
  PendienteCorreccion: 'Pendiente de corrección',
  Corregida: 'Corregida',
  CorreccionManual: 'Corrección manual',
};

export const esRoja = (alerta: Pick<AlertaFocoDetalle, 'severidad'>) => alerta.severidad.toLowerCase().startsWith('roj');

export const nombreCompleto = (usuario: Pick<UsuarioResumen, 'nombre' | 'apellido'>) =>
  `${usuario.apellido}, ${usuario.nombre}`;

/** Cuando Identidad no devuelve el nombre se muestra algo reconocible en vez de un UUID entero. */
export const nombreDeReemplazo = (alumnoId: string) => `Alumno ···${alumnoId.slice(-4)}`;
