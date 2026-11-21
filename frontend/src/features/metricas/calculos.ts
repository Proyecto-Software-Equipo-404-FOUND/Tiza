import { formatearNumero } from '@/features/revision-notas/formato';
import type { MetricasEvaluacion, MetricaPregunta } from './tipos';

export type Diferencia = { texto: string; tono: 'arriba' | 'abajo' | 'igual' };

/** 2,5 → «2,5 puntos por encima del promedio»; −0,8 → «0,8 puntos por debajo del promedio»; 0 → «En el promedio». */
export function describirDiferencia(diferencia: number): Diferencia {
  if (Math.abs(diferencia) < 0.005) return { texto: 'En el promedio', tono: 'igual' };
  const valor = formatearNumero(Math.abs(diferencia));
  const puntos = `${valor} ${valor === '1' ? 'punto' : 'puntos'}`;
  return diferencia > 0
    ? { texto: `${puntos} por encima del promedio`, tono: 'arriba' }
    : { texto: `${puntos} por debajo del promedio`, tono: 'abajo' };
}

/** Ancho de la barra de una pregunta: el porcentaje redondeado y acotado entre 0 y 100. */
export const porcentajeParaBarra = (porcentaje: number) => Math.min(100, Math.max(0, Math.round(porcentaje)));

/** La pregunta que el servidor marca como la más difícil, si está en la lista. */
export const preguntaMasDificil = (metricas: MetricasEvaluacion): MetricaPregunta | undefined =>
  metricas.preguntaMasDificilId ? metricas.preguntas.find((p) => p.preguntaId === metricas.preguntaMasDificilId) : undefined;
