import { nuevoUuid } from '@/shared/lib/uuid';
import type { Evaluacion, Pregunta, PreguntasJson, TipoPregunta } from './tipos';

/*
 * Modelo del formulario de borrador de evaluaciones.
 * Los números se almacenan como string mientras el usuario escribe en los inputs
 * y se convierten a number al serializar y enviar la petición (aDatosEvaluacion).
 */

export const TIPOS_EVALUACION = ['Examen', 'Cuestionario', 'Quiz'] as const;

export type OpcionBorrador = { id: string; texto: string; esCorrecta: boolean };
export type CriterioBorrador = { clave: string; descripcion: string; puntaje: string };

/* Modelos específicos para dinámicas gamificadas en el borrador */
export type CestaBorrador = { id: string; titulo: string; icono: string };
export type ItemBucketBorrador = { id: string; texto: string; cestaCorrecta: string };
export type AfirmacionBorrador = { id: string; texto: string; esVerdadera: boolean };
export type ParBorrador = {
  id: string;
  conceptoTexto: string;
  definicionTexto: string;
};
export type ItemOrdenBorrador = { id: string; texto: string; ordenCorrecto: number };

export type PreguntaBorrador = {
  id: string;
  enunciado: string;
  tipo: TipoPregunta;
  puntajeMaximo: string;
  /* Puntaje editable por ítem para dinámicas con calificación granular (0.3 por defecto) */
  puntajePorItem: string;
  opciones: OpcionBorrador[];
  criterios: CriterioBorrador[];
  cestas: CestaBorrador[];
  itemsBucket: ItemBucketBorrador[];
  afirmaciones: AfirmacionBorrador[];
  pares: ParBorrador[];
  itemsOrden: ItemOrdenBorrador[];
};

export type EvaluacionBorrador = { titulo: string; tipo: string; preguntas: PreguntaBorrador[] };

/* Lo que reciben POST y PUT de /api/v1/evaluaciones (EvaluacionRequest del contrato) */
export type DatosEvaluacion = { titulo: string; tipo: string; preguntasJson: PreguntasJson };

/* Errores por campo. La clave es «titulo» o «<id de la pregunta/opción/criterio>.<campo>». */
export type ErroresBorrador = Record<string, string>;

export const nuevaOpcion = (): OpcionBorrador => ({ id: nuevoUuid(), texto: '', esCorrecta: false });
export const nuevoCriterio = (): CriterioBorrador => ({ clave: nuevoUuid(), descripcion: '', puntaje: '' });

/* Plantillas precargadas por defecto con conceptos de Redes y Telecomunicaciones para la demo */
const crearPlantillaBucketSort = () => ({
  enunciado: 'Clasificá cada protocolo según su capa en el modelo TCP/IP:',
  puntajePorItem: '0.3',
  puntajeMaximo: '1.5',
  cestas: [
    { id: 'capa_aplicacion', titulo: 'Capa de Aplicación', icono: '🌐' },
    { id: 'capa_red_transporte', titulo: 'Red y Transporte', icono: '🔀' },
  ],
  itemsBucket: [
    { id: nuevoUuid(), texto: 'HTTP / HTTPS', cestaCorrecta: 'capa_aplicacion' },
    { id: nuevoUuid(), texto: 'Protocolo IP', cestaCorrecta: 'capa_red_transporte' },
    { id: nuevoUuid(), texto: 'DNS', cestaCorrecta: 'capa_aplicacion' },
    { id: nuevoUuid(), texto: 'TCP / UDP', cestaCorrecta: 'capa_red_transporte' },
    { id: nuevoUuid(), texto: 'SMTP (Correo)', cestaCorrecta: 'capa_aplicacion' },
  ],
});

const crearPlantillaClasificadorRapido = () => ({
  enunciado: 'Determiná si cada afirmación sobre telecomunicaciones y modulación es Verdadera o Falsa:',
  puntajePorItem: '0.3',
  puntajeMaximo: '1.5',
  afirmaciones: [
    { id: nuevoUuid(), texto: 'La modulación AM varía la amplitud de la portadora manteniendo su frecuencia constante.', esVerdadera: true },
    { id: nuevoUuid(), texto: 'En el modelo OSI, la dirección MAC opera en la Capa de Red (Capa 3).', esVerdadera: false },
    { id: nuevoUuid(), texto: 'La fibra óptica transmite señales en el espectro de la luz visible o infrarroja.', esVerdadera: true },
    { id: nuevoUuid(), texto: 'A mayor frecuencia de una onda electromagnética, mayor es su longitud de onda.', esVerdadera: false },
    { id: nuevoUuid(), texto: 'El espectro electromagnético de radiofrecuencia es un recurso natural finito y regulado.', esVerdadera: true },
  ],
});

const crearPlantillaEmparejamiento = () => ({
  enunciado: 'Asociá cada concepto de modulación y capa física con su definición correspondiente:',
  puntajePorItem: '0.3',
  puntajeMaximo: '1.5',
  pares: [
    { id: nuevoUuid(), conceptoTexto: 'Modulación QAM', definicionTexto: 'Combina variación de amplitud y fase de la señal portadora.' },
    { id: nuevoUuid(), conceptoTexto: 'Ancho de Banda', definicionTexto: 'Rango de frecuencias que puede transportar el canal de comunicación.' },
    { id: nuevoUuid(), conceptoTexto: 'Capa Física', definicionTexto: 'Transmisión de cadenas de bits sobre el medio de transmisión.' },
    { id: nuevoUuid(), conceptoTexto: 'Modulación FM', definicionTexto: 'Varía la frecuencia de la portadora según la señal moduladora.' },
    { id: nuevoUuid(), conceptoTexto: 'Atenuación', definicionTexto: 'Pérdida paulatina de potencia de la señal con la distancia recorrida.' },
  ],
});

const crearPlantillaOrdenamiento = () => ({
  enunciado: 'Ordená las capas del Modelo OSI desde la capa inferior (Capa 1) hacia la superior (Capa 4):',
  puntajePorItem: '1',
  puntajeMaximo: '1',
  itemsOrden: [
    { id: nuevoUuid(), texto: 'Capa 1: Capa Física (señales eléctricas/ópticas y bits)', ordenCorrecto: 1 },
    { id: nuevoUuid(), texto: 'Capa 2: Capa de Enlace de Datos (tramas y direcciones MAC)', ordenCorrecto: 2 },
    { id: nuevoUuid(), texto: 'Capa 3: Capa de Red (paquetes y enrutamiento IP)', ordenCorrecto: 3 },
    { id: nuevoUuid(), texto: 'Capa 4: Capa de Transporte (segmentos, TCP/UDP)', ordenCorrecto: 4 },
  ],
});

export const nuevaPregunta = (tipo: TipoPregunta = 'OpcionMultiple'): PreguntaBorrador => {
  const base = {
    id: nuevoUuid(),
    enunciado: '',
    tipo,
    puntajeMaximo: '1',
    puntajePorItem: '0.3',
    opciones: [nuevaOpcion(), nuevaOpcion()],
    criterios: [],
    cestas: [],
    itemsBucket: [],
    afirmaciones: [],
    pares: [],
    itemsOrden: [],
  };

  if (tipo === 'BucketSort') {
    return { ...base, ...crearPlantillaBucketSort() };
  }
  if (tipo === 'ClasificadorRapido') {
    return { ...base, ...crearPlantillaClasificadorRapido() };
  }
  if (tipo === 'Emparejamiento') {
    return { ...base, ...crearPlantillaEmparejamiento() };
  }
  if (tipo === 'Ordenamiento') {
    return { ...base, ...crearPlantillaOrdenamiento() };
  }
  return base;
};

export const borradorVacio = (): EvaluacionBorrador => ({ titulo: '', tipo: TIPOS_EVALUACION[0], preguntas: [] });

export function borradorDesde(evaluacion: Evaluacion): EvaluacionBorrador {
  return {
    titulo: evaluacion.titulo,
    tipo: evaluacion.tipo,
    preguntas: evaluacion.preguntasJson.preguntas.map((pregunta) => ({
      id: pregunta.id,
      enunciado: pregunta.enunciado,
      tipo: pregunta.tipo,
      puntajeMaximo: String(pregunta.puntajeMaximo),
      puntajePorItem: String(pregunta.puntajePorItem ?? (pregunta.tipo === 'Ordenamiento' ? 1 : 0.3)),
      opciones:
        pregunta.opciones && pregunta.opciones.length > 0
          ? pregunta.opciones.map((opcion) => ({ ...opcion }))
          : [nuevaOpcion(), nuevaOpcion()],
      criterios: (pregunta.rubrica?.criterios ?? []).map((criterio) => ({
        clave: nuevoUuid(),
        descripcion: criterio.descripcion,
        puntaje: String(criterio.puntaje),
      })),
      cestas: pregunta.cestas ? pregunta.cestas.map((c) => ({ ...c })) : [],
      itemsBucket: pregunta.itemsBucket ? pregunta.itemsBucket.map((i) => ({ ...i })) : [],
      afirmaciones: pregunta.afirmaciones ? pregunta.afirmaciones.map((a) => ({ ...a })) : [],
      pares: pregunta.pares
        ? pregunta.pares.map((p) => ({
            id: nuevoUuid(),
            conceptoTexto: p.conceptoTexto,
            definicionTexto: p.definicionTexto,
          }))
        : [],
      itemsOrden: pregunta.itemsOrden ? pregunta.itemsOrden.map((io) => ({ ...io })) : [],
    })),
  };
}

const esNumeroValido = (texto: string) => texto.trim() !== '' && Number.isFinite(Number(texto)) && Number(texto) >= 0;

/* Evita errores de coma flotante al sumar decimales (0.1 + 0.2) */
export const redondearPuntos = (valor: number) => Math.round(valor * 100) / 100;

export const sumaCriterios = (criterios: CriterioBorrador[]) =>
  criterios.reduce((suma, criterio) => suma + (esNumeroValido(criterio.puntaje) ? Number(criterio.puntaje) : 0), 0);

/* Puntos del máximo que los criterios todavía no repartieron (negativo si se pasaron) */
export const puntosSinRepartir = (pregunta: PreguntaBorrador) =>
  redondearPuntos(Number(pregunta.puntajeMaximo) - sumaCriterios(pregunta.criterios));

export function validar(borrador: EvaluacionBorrador): ErroresBorrador {
  const errores: ErroresBorrador = {};

  const titulo = borrador.titulo.trim();
  if (!titulo) errores.titulo = 'Ingresá un título';
  else if (titulo.length > 200) errores.titulo = 'Máximo 200 caracteres';

  for (const pregunta of borrador.preguntas) {
    if (!pregunta.enunciado.trim()) errores[`${pregunta.id}.enunciado`] = 'Escribí el enunciado';
    if (!esNumeroValido(pregunta.puntajeMaximo)) errores[`${pregunta.id}.puntaje`] = 'Ingresá un puntaje de 0 o más';

    if (pregunta.tipo === 'OpcionMultiple') {
      if (pregunta.opciones.length < 2) errores[`${pregunta.id}.opciones`] = 'Agregá al menos 2 opciones';
      else if (!pregunta.opciones.some((opcion) => opcion.esCorrecta))
        errores[`${pregunta.id}.opciones`] = 'Marcá cuál es la opción correcta';
      for (const opcion of pregunta.opciones) {
        if (!opcion.texto.trim()) errores[`${opcion.id}.texto`] = 'Escribí el texto de la opción';
      }
    } else if (pregunta.tipo === 'Abierta') {
      for (const criterio of pregunta.criterios) {
        if (!criterio.descripcion.trim()) errores[`${criterio.clave}.descripcion`] = 'Describí el criterio';
        if (!esNumeroValido(criterio.puntaje)) errores[`${criterio.clave}.puntaje`] = 'Ingresá un puntaje de 0 o más';
      }
      /* Con rúbrica, los criterios reparten todo el puntaje de la pregunta */
      if (pregunta.criterios.length > 0 && esNumeroValido(pregunta.puntajeMaximo)) {
        const diferencia = puntosSinRepartir(pregunta);
        if (diferencia < 0) errores[`${pregunta.id}.criterios`] = 'Los criterios suman más que el puntaje máximo de la pregunta';
        if (diferencia > 0) errores[`${pregunta.id}.criterios`] = 'Los criterios tienen que sumar el puntaje máximo de la pregunta';
      }
    } else if (pregunta.tipo === 'BucketSort') {
      if (pregunta.cestas.length < 2) errores[`${pregunta.id}.cestas`] = 'Definí al menos 2 cestas';
      if (pregunta.itemsBucket.length === 0) errores[`${pregunta.id}.items`] = 'Agregá al menos una ficha para clasificar';
    } else if (pregunta.tipo === 'ClasificadorRapido') {
      if (pregunta.afirmaciones.length === 0) errores[`${pregunta.id}.afirmaciones`] = 'Agregá al menos una afirmación';
    } else if (pregunta.tipo === 'Emparejamiento') {
      if (pregunta.pares.length < 2) errores[`${pregunta.id}.pares`] = 'Agregá al menos 2 pares para asociar';
    } else if (pregunta.tipo === 'Ordenamiento') {
      if (pregunta.itemsOrden.length < 2) errores[`${pregunta.id}.itemsOrden`] = 'Agregá al menos 2 pasos a ordenar';
    }
  }
  return errores;
}

/* Arma el cuerpo del contrato. Solo se envía lo que corresponde al tipo de cada pregunta */
export function aDatosEvaluacion(borrador: EvaluacionBorrador): DatosEvaluacion {
  const preguntas: Pregunta[] = borrador.preguntas.map((pregunta) => {
    const base: Pregunta = {
      id: pregunta.id,
      enunciado: pregunta.enunciado.trim(),
      tipo: pregunta.tipo,
      puntajeMaximo: Number(pregunta.puntajeMaximo),
    };

    if (pregunta.tipo === 'OpcionMultiple') {
      return {
        ...base,
        opciones: pregunta.opciones.map((opcion) => ({
          id: opcion.id,
          texto: opcion.texto.trim(),
          esCorrecta: opcion.esCorrecta,
        })),
      };
    }

    if (pregunta.tipo === 'BucketSort') {
      return {
        ...base,
        puntajePorItem: esNumeroValido(pregunta.puntajePorItem) ? Number(pregunta.puntajePorItem) : 0.3,
        cestas: pregunta.cestas.map((c) => ({ ...c })),
        itemsBucket: pregunta.itemsBucket.map((i) => ({ ...i })),
      };
    }

    if (pregunta.tipo === 'ClasificadorRapido') {
      return {
        ...base,
        puntajePorItem: esNumeroValido(pregunta.puntajePorItem) ? Number(pregunta.puntajePorItem) : 0.3,
        afirmaciones: pregunta.afirmaciones.map((a) => ({ ...a })),
      };
    }

    if (pregunta.tipo === 'Emparejamiento') {
      return {
        ...base,
        puntajePorItem: esNumeroValido(pregunta.puntajePorItem) ? Number(pregunta.puntajePorItem) : 0.3,
        pares: pregunta.pares.map((p, idx) => ({
          conceptoId: `c_${idx + 1}`,
          conceptoTexto: p.conceptoTexto.trim(),
          definicionId: `d_${idx + 1}`,
          definicionTexto: p.definicionTexto.trim(),
        })),
      };
    }

    if (pregunta.tipo === 'Ordenamiento') {
      return {
        ...base,
        itemsOrden: pregunta.itemsOrden.map((io, idx) => ({
          id: io.id,
          texto: io.texto.trim(),
          ordenCorrecto: idx + 1,
        })),
      };
    }

    /* Tipo Abierta */
    if (pregunta.criterios.length === 0) return base;
    return {
      ...base,
      rubrica: {
        criterios: pregunta.criterios.map((criterio) => ({
          descripcion: criterio.descripcion.trim(),
          puntaje: Number(criterio.puntaje),
        })),
      },
    };
  });
  return { titulo: borrador.titulo.trim(), tipo: borrador.tipo, preguntasJson: { version: 1, preguntas } };
}
