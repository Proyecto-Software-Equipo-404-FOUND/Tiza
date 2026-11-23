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
  Corregida: 'Nota sugerida',
  CorreccionManual: 'Corrección manual',
};

export const esRoja = (alerta: Pick<AlertaFocoDetalle, 'severidad'>) => alerta.severidad.toLowerCase().startsWith('roj');

export const nombreCompleto = (usuario: Pick<UsuarioResumen, 'nombre' | 'apellido'>) =>
  `${usuario.apellido}, ${usuario.nombre}`;

/** Cuando Identidad no devuelve el nombre se muestra algo reconocible en vez de un UUID entero. */
export const nombreDeReemplazo = (alumnoId: string) => `Alumno ···${alumnoId.slice(-4)}`;

/** Alertas de foco que ocurrieron mientras el alumno estaba en una pregunta. */
export const alertasDePregunta = (alertas: AlertaFocoDetalle[], preguntaId: string) =>
  alertas.filter((alerta) => alerta.preguntaId === preguntaId);

/** «Pregunta 2», o «Fuera de una pregunta» si la alerta no trae pregunta (o no coincide con ninguna). */
export function etiquetaPreguntaDeAlerta(alerta: AlertaFocoDetalle, preguntaIds: string[]) {
  const indice = alerta.preguntaId ? preguntaIds.indexOf(alerta.preguntaId) : -1;
  return indice === -1 ? 'Fuera de una pregunta' : `Pregunta ${indice + 1}`;
}

/** «Salió 2 veces (1 roja) · 1 min 5 s en total». */
export function resumenSalidas(alertas: AlertaFocoDetalle[]) {
  const rojas = alertas.filter(esRoja).length;
  const total = alertas.reduce((suma, alerta) => suma + alerta.duracionMs, 0);
  const veces = alertas.length === 1 ? 'Salió 1 vez' : `Salió ${alertas.length} veces`;
  // La insignia ya dice la peor gravedad: solo se aclara cuántas son rojas cuando hay mezcla
  const hayMezcla = rojas > 0 && rojas < alertas.length;
  const gravedad = !hayMezcla ? '' : rojas === 1 ? ' (1 roja)' : ` (${rojas} rojas)`;
  return `${veces}${gravedad} de la pantalla · ${formatearDuracion(total)} en total`;
}
