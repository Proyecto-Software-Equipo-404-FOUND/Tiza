// Tipos según contracts/openapi/evaluaciones-v1.json y contracts/schemas/preguntas-v1.schema.json
export type Criterio = { descripcion: string; puntaje: number };
export type Rubrica = { criterios?: Criterio[] };
export type Opcion = { id: string; texto: string; esCorrecta: boolean };

export type Pregunta = {
  id: string;
  enunciado: string;
  tipo: 'OpcionMultiple' | 'Abierta';
  puntajeMaximo: number;
  opciones?: Opcion[];
  rubrica?: Rubrica;
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
