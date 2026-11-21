// Forma de GET /api/v1/alumnos/me/evaluaciones (MiExamen en contracts/openapi/evaluaciones-v1.json)
export type EstadoMiExamen = 'PendienteCorreccion' | 'Aprobada';

export type MiExamen = {
  evaluacionId: string;
  entregaId: string;
  cohorteId?: string | null;
  titulo: string;
  fechaEntregaUtc: string;
  estado: EstadoMiExamen;
  /** Solo viene si estado = Aprobada */
  notaFinal?: number | null;
  /** Solo viene si estado = Aprobada */
  puntajeMaximo?: number | null;
};
