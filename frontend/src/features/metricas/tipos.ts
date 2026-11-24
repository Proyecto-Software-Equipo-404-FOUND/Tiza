// Forma de GET /api/v1/evaluaciones/{id}/metricas (MetricasEvaluacion en contracts/openapi/evaluaciones-v1.json, S3-EV-05)
// Solo cuenta entregas con la nota aprobada. Con 0 aprobadas, `promedio` y `preguntaMasDificilId` vienen null y las listas vacías.
export type MetricaPregunta = {
  preguntaId: string;
  enunciado: string;
  puntajeMaximo: number;
  promedio: number;
  /** promedio / puntajeMaximo × 100 */
  porcentaje: number;
};

export type MetricaAlumno = {
  alumnoId: string;
  notaFinal: number;
  /** notaFinal − promedio (puede ser negativa) */
  diferenciaConPromedio: number;
};

export type MetricasEvaluacion = {
  evaluacionId: string;
  cantidadEntregas: number;
  cantidadAprobadas: number;
  puntajeMaximo: number;
  promedio?: number | null;
  /** La de menor porcentaje promedio obtenido */
  preguntaMasDificilId?: string | null;
  preguntas: MetricaPregunta[];
  alumnos: MetricaAlumno[];
};
