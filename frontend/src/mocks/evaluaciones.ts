import { http, HttpResponse } from 'msw';
import { COHORTE_1_ID, INSTITUCION_A } from './datos';
import type { Evaluacion } from '@/features/evaluaciones/tipos';

// Datos falsos con la forma de contracts/openapi/evaluaciones-v1.json
export const evaluaciones: Evaluacion[] = [
  {
    id: 'e0000000-0000-4000-8000-000000000001',
    institucionId: INSTITUCION_A,
    titulo: 'Parcial 1: Variables y estructuras de control',
    tipo: 'Examen',
    estado: 'Publicada',
    cohorteId: COHORTE_1_ID,
    preguntasJson: {
      version: 1,
      preguntas: [
        {
          id: 'f0000000-0000-4000-8000-000000000001',
          enunciado: '¿Qué imprime un bucle for que va de 1 a 3 sumando 1 en cada vuelta?',
          tipo: 'OpcionMultiple',
          puntajeMaximo: 2,
          opciones: [
            { id: 'a0000000-0000-4000-8000-000000000001', texto: '1 2 3', esCorrecta: true },
            { id: 'a0000000-0000-4000-8000-000000000002', texto: '0 1 2', esCorrecta: false },
            { id: 'a0000000-0000-4000-8000-000000000003', texto: '1 2 3 4', esCorrecta: false },
          ],
        },
        {
          id: 'f0000000-0000-4000-8000-000000000002',
          enunciado: 'Explicá la diferencia entre una variable y una constante.',
          tipo: 'Abierta',
          puntajeMaximo: 8,
          rubrica: {
            criterios: [
              { descripcion: 'Define correctamente variable', puntaje: 3 },
              { descripcion: 'Define correctamente constante', puntaje: 3 },
              { descripcion: 'Da un ejemplo de cada una', puntaje: 2 },
            ],
          },
        },
      ],
    },
    fechaCreacion: '2026-10-20T14:00:00Z',
    fechaActualizacion: '2026-10-22T09:30:00Z',
    version: 3,
  },
  {
    id: 'e0000000-0000-4000-8000-000000000002',
    institucionId: INSTITUCION_A,
    titulo: 'Trabajo práctico: Funciones',
    tipo: 'Cuestionario',
    estado: 'Borrador',
    cohorteId: null,
    preguntasJson: {
      version: 1,
      preguntas: [
        {
          id: 'f0000000-0000-4000-8000-000000000003',
          enunciado: 'Escribí una función que devuelva el mayor de dos números.',
          tipo: 'Abierta',
          puntajeMaximo: 10,
          rubrica: { criterios: [{ descripcion: 'La función es correcta', puntaje: 10 }] },
        },
      ],
    },
    fechaCreacion: '2026-10-24T10:00:00Z',
    fechaActualizacion: '2026-10-24T10:00:00Z',
    version: 1,
  },
  {
    id: 'e0000000-0000-4000-8000-000000000003',
    institucionId: INSTITUCION_A,
    titulo: 'Quiz 1: Redes y Telecomunicaciones',
    tipo: 'Quiz',
    estado: 'Borrador',
    cohorteId: null,
    preguntasJson: {
      version: 1,
      preguntas: [
        {
          id: 'f0000000-0000-4000-8000-000000000010',
          enunciado: 'Clasificá cada protocolo según su capa en el modelo TCP/IP:',
          tipo: 'BucketSort',
          puntajeMaximo: 1.5,
          puntajePorItem: 0.3,
          cestas: [
            { id: 'capa_aplicacion', titulo: 'Capa de Aplicación', icono: '🌐' },
            { id: 'capa_red_transporte', titulo: 'Red y Transporte', icono: '🔀' },
          ],
          itemsBucket: [
            { id: 'it_http', texto: 'HTTP / HTTPS', cestaCorrecta: 'capa_aplicacion' },
            { id: 'it_ip', texto: 'Protocolo IP', cestaCorrecta: 'capa_red_transporte' },
            { id: 'it_dns', texto: 'DNS', cestaCorrecta: 'capa_aplicacion' },
            { id: 'it_tcp', texto: 'TCP / UDP', cestaCorrecta: 'capa_red_transporte' },
            { id: 'it_smtp', texto: 'SMTP (Correo)', cestaCorrecta: 'capa_aplicacion' },
          ],
        },
        {
          id: 'f0000000-0000-4000-8000-000000000011',
          enunciado: 'Determiná si cada afirmación sobre telecomunicaciones y modulación es Verdadera o Falsa:',
          tipo: 'ClasificadorRapido',
          puntajeMaximo: 1.5,
          puntajePorItem: 0.3,
          afirmaciones: [
            { id: 'af_1', texto: 'La modulación AM varía la amplitud de la portadora manteniendo su frecuencia constante.', esVerdadera: true },
            { id: 'af_2', texto: 'En el modelo OSI, la dirección MAC opera en la Capa de Red (Capa 3).', esVerdadera: false },
            { id: 'af_3', texto: 'La fibra óptica transmite señales en el espectro de la luz visible o infrarroja.', esVerdadera: true },
            { id: 'af_4', texto: 'A mayor frecuencia de una onda electromagnética, mayor es su longitud de onda.', esVerdadera: false },
            { id: 'af_5', texto: 'El espectro electromagnético de radiofrecuencia es un recurso natural finito y regulado.', esVerdadera: true },
          ],
        },
        {
          id: 'f0000000-0000-4000-8000-000000000012',
          enunciado: 'Asociá cada concepto de modulación y capa física con su definición correspondiente:',
          tipo: 'Emparejamiento',
          puntajeMaximo: 1.5,
          puntajePorItem: 0.3,
          pares: [
            { conceptoId: 'c1', conceptoTexto: 'Modulación QAM', definicionId: 'd1', definicionTexto: 'Combina variación de amplitud y fase de la portadora.' },
            { conceptoId: 'c2', conceptoTexto: 'Ancho de Banda', definicionId: 'd2', definicionTexto: 'Rango de frecuencias que puede transportar el canal.' },
            { conceptoId: 'c3', conceptoTexto: 'Capa Física', definicionId: 'd3', definicionTexto: 'Transmisión de cadenas de bits sobre el medio de transmisión.' },
            { conceptoId: 'c4', conceptoTexto: 'Modulación FM', definicionId: 'd4', definicionTexto: 'Varía la frecuencia de la portadora según la moduladora.' },
            { conceptoId: 'c5', conceptoTexto: 'Atenuación', definicionId: 'd5', definicionTexto: 'Pérdida paulatina de potencia de la señal con la distancia.' },
          ],
        },
        {
          id: 'f0000000-0000-4000-8000-000000000013',
          enunciado: 'Ordená las capas del Modelo OSI desde la capa inferior (Capa 1) hacia la superior (Capa 4):',
          tipo: 'Ordenamiento',
          puntajeMaximo: 1,
          itemsOrden: [
            { id: 'l1', texto: 'Capa 1: Capa Física (señales eléctricas/ópticas y bits)', ordenCorrecto: 1 },
            { id: 'l2', texto: 'Capa 2: Capa de Enlace de Datos (tramas y direcciones MAC)', ordenCorrecto: 2 },
            { id: 'l3', texto: 'Capa 3: Capa de Red (paquetes y enrutamiento IP)', ordenCorrecto: 3 },
            { id: 'l4', texto: 'Capa 4: Capa de Transporte (segmentos, TCP/UDP)', ordenCorrecto: 4 },
          ],
        },
        {
          id: 'f0000000-0000-4000-8000-000000000014',
          enunciado: '¿Qué protocolo de la capa de transporte proporciona una entrega confiable y orientada a la conexión?',
          tipo: 'OpcionMultiple',
          puntajeMaximo: 2,
          opciones: [
            { id: 'op_tcp', texto: 'TCP (Transmission Control Protocol)', esCorrecta: true },
            { id: 'op_udp', texto: 'UDP (User Datagram Protocol)', esCorrecta: false },
            { id: 'op_icmp', texto: 'ICMP (Internet Control Message Protocol)', esCorrecta: false },
          ],
        },
      ],
    },
    fechaCreacion: '2026-11-20T10:00:00Z',
    fechaActualizacion: '2026-11-20T10:00:00Z',
    version: 1,
  },
];

// Como el servicio real: sin X-Institucion-Id responde 400 y solo muestra lo de esa institución
function institucionDe(request: Request): string | null {
  const id = request.headers.get('X-Institucion-Id');
  return id && id !== '00000000-0000-0000-0000-000000000000' ? id : null;
}

const sinInstitucion = () =>
  HttpResponse.json({ title: 'Petición inválida', status: 400, detail: 'Falta la institución.' }, { status: 400 });

export const handlersEvaluaciones = [
  // Crea en estado Borrador y responde 201 con Location, como el servicio real
  http.post('*/api/v1/evaluaciones', async ({ request }) => {
    const institucionId = institucionDe(request);
    if (!institucionId) return sinInstitucion();
    const { titulo, tipo, preguntasJson } = (await request.json()) as Pick<Evaluacion, 'titulo' | 'tipo' | 'preguntasJson'>;
    if (!titulo?.trim() || !tipo?.trim() || !preguntasJson) {
      return HttpResponse.json(
        { title: 'Petición inválida', status: 400, detail: 'Faltan datos de la evaluación.' },
        { status: 400 },
      );
    }
    const ahora = new Date().toISOString();
    const nueva: Evaluacion = {
      id: crypto.randomUUID(),
      institucionId,
      titulo,
      tipo,
      estado: 'Borrador',
      cohorteId: null,
      preguntasJson,
      fechaCreacion: ahora,
      fechaActualizacion: ahora,
      version: 1,
    };
    evaluaciones.push(nueva);
    return HttpResponse.json(nueva, { status: 201, headers: { Location: `/api/v1/evaluaciones/${nueva.id}` } });
  }),

  // Reglas del contrato, en orden: cohorte obligatoria (400), inexistente (404), versión distinta (409),
  // estado distinto de Borrador (409), sin preguntas (400)
  http.post('*/api/v1/evaluaciones/:id/publicar', async ({ params, request }) => {
    const institucionId = institucionDe(request);
    if (!institucionId) return sinInstitucion();
    const { versionEsperada, cohorteId } = (await request.json()) as { versionEsperada?: number; cohorteId?: string };
    if (!cohorteId) {
      return HttpResponse.json(
        { title: 'Petición inválida', status: 400, detail: 'La cohorte es obligatoria.' },
        { status: 400 },
      );
    }
    const evaluacion = evaluaciones.find((e) => e.id === params.id && e.institucionId === institucionId);
    if (!evaluacion) return HttpResponse.json({ title: 'No encontrada', status: 404 }, { status: 404 });
    if (versionEsperada !== evaluacion.version) {
      return HttpResponse.json(
        { title: 'Conflicto', status: 409, detail: 'La evaluación cambió. Recargala e intentá de nuevo.' },
        { status: 409 },
      );
    }
    if (evaluacion.estado !== 'Borrador') {
      return HttpResponse.json(
        { title: 'Conflicto', status: 409, detail: 'Solo se pueden publicar evaluaciones en estado Borrador.' },
        { status: 409 },
      );
    }
    if (evaluacion.preguntasJson.preguntas.length === 0) {
      return HttpResponse.json(
        { title: 'Petición inválida', status: 400, detail: 'La evaluación no tiene preguntas.' },
        { status: 400 },
      );
    }
    Object.assign(evaluacion, {
      estado: 'Publicada',
      cohorteId,
      version: evaluacion.version + 1,
      fechaActualizacion: new Date().toISOString(),
    });
    return HttpResponse.json(evaluacion);
  }),

  // Solo se borra un Borrador: si no, 409 como en el contrato
  http.delete('*/api/v1/evaluaciones/:id', ({ params, request }) => {
    const institucionId = institucionDe(request);
    if (!institucionId) return sinInstitucion();
    const indice = evaluaciones.findIndex((e) => e.id === params.id && e.institucionId === institucionId);
    if (indice === -1) return HttpResponse.json({ title: 'No encontrada', status: 404 }, { status: 404 });
    if (evaluaciones[indice].estado !== 'Borrador') {
      return HttpResponse.json(
        {
          title: 'La operación no se puede realizar en el estado actual.',
          status: 409,
          detail: 'Solo se pueden borrar evaluaciones en estado Borrador.',
        },
        { status: 409 },
      );
    }
    evaluaciones.splice(indice, 1);
    return new HttpResponse(null, { status: 204 });
  }),

  // Solo se modifica un Borrador. Truco para probar el 409 en pantalla: ponerle de título «conflicto».
  // El contrato no dice si el PUT sube la versión: acá se deja igual.
  http.put('*/api/v1/evaluaciones/:id', async ({ params, request }) => {
    const institucionId = institucionDe(request);
    if (!institucionId) return sinInstitucion();
    const evaluacion = evaluaciones.find((e) => e.id === params.id && e.institucionId === institucionId);
    if (!evaluacion) return HttpResponse.json({ title: 'No encontrada', status: 404 }, { status: 404 });

    const { titulo, tipo, preguntasJson } = (await request.json()) as Pick<Evaluacion, 'titulo' | 'tipo' | 'preguntasJson'>;
    if (evaluacion.estado !== 'Borrador' || titulo?.trim().toLowerCase() === 'conflicto') {
      return HttpResponse.json(
        {
          title: 'La operación no se puede realizar en el estado actual.',
          status: 409,
          detail: 'Solo se pueden modificar evaluaciones en estado Borrador.',
        },
        { status: 409 },
      );
    }
    Object.assign(evaluacion, { titulo, tipo, preguntasJson, fechaActualizacion: new Date().toISOString() });
    return HttpResponse.json(evaluacion);
  }),

  http.get('*/api/v1/evaluaciones', ({ request }) => {
    const institucionId = institucionDe(request);
    if (!institucionId) return sinInstitucion();
    return HttpResponse.json(evaluaciones.filter((e) => e.institucionId === institucionId));
  }),

  http.get('*/api/v1/evaluaciones/:id', ({ params, request }) => {
    const institucionId = institucionDe(request);
    if (!institucionId) return sinInstitucion();
    const evaluacion = evaluaciones.find((e) => e.id === params.id && e.institucionId === institucionId);
    return evaluacion
      ? HttpResponse.json(evaluacion)
      : HttpResponse.json({ title: 'No encontrada', status: 404 }, { status: 404 });
  }),
];
