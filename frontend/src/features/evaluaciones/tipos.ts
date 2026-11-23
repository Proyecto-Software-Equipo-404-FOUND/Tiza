/* Tipos según contracts/openapi/evaluaciones-v1.json y contracts/schemas/preguntas-v1.schema.json */
export type Criterio = { descripcion: string; puntaje: number };
export type Rubrica = { criterios?: Criterio[] };
export type Opcion = { id: string; texto: string; esCorrecta: boolean };

/* Tipos específicos para dinámicas gamificadas de Quiz */
export type Cesta = { id: string; titulo: string; icono: string };
export type ItemBucket = { id: string; texto: string; cestaCorrecta: string };
export type Afirmacion = { id: string; texto: string; esVerdadera: boolean };
export type ParEmparejamiento = {
  conceptoId: string;
  conceptoTexto: string;
  definicionId: string;
  definicionTexto: string;
};
export type ItemOrdenamiento = { id: string; texto: string; ordenCorrecto: number };

export type TipoPregunta =
  | 'OpcionMultiple'
  | 'Abierta'
  | 'BucketSort'
  | 'ClasificadorRapido'
  | 'Emparejamiento'
  | 'Ordenamiento';

export type Pregunta = {
  id: string;
  enunciado: string;
  tipo: TipoPregunta;
  puntajeMaximo: number;
  /* Puntaje editable por ítem para dinámicas con calificación granular */
  puntajePorItem?: number;
  opciones?: Opcion[];
  rubrica?: Rubrica;
  /* Configuración de dinámicas gamificadas */
  cestas?: Cesta[];
  itemsBucket?: ItemBucket[];
  afirmaciones?: Afirmacion[];
  pares?: ParEmparejamiento[];
  itemsOrden?: ItemOrdenamiento[];
};

export type PreguntasJson = { version: number; preguntas: Pregunta[] };

export type EstadoEvaluacion = 'Borrador' | 'Publicada';

export type Evaluacion = {
  id: string;
  institucionId: string;
  titulo: string;
  tipo: string;
  estado: EstadoEvaluacion;
  cohorteId?: string | null;
  preguntasJson: PreguntasJson;
  fechaCreacion: string;
  fechaActualizacion: string;
  version: number;
};
