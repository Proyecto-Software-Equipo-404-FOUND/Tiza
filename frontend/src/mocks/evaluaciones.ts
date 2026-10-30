import { http, HttpResponse } from 'msw';
import { COHORTE_1_ID, INSTITUCION_A } from './datos';
import type { Evaluacion } from '@/features/evaluaciones/tipos';

// Datos falsos con la forma de contracts/openapi/evaluaciones-v1.json
const evaluaciones: Evaluacion[] = [
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
