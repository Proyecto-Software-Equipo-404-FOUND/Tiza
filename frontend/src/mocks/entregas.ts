import { http, HttpResponse } from 'msw';
import { ALUMNO_1_A_ID, ALUMNO_2_A_ID } from './datos';
import type { EntregaDetalle, EntregaResumen, UsuarioResumen } from '@/features/revision-notas/tipos';

// Datos falsos con la forma de contracts/openapi/evaluaciones-v1.json (Resultados) y identidad-v1.json
const EVALUACION_ID = 'e0000000-0000-4000-8000-000000000001';
const SALA_ID = '6a2b7c3d-8e9f-4a01-b2c3-d4e5f6a7b8c9';
const P_MC = 'f0000000-0000-4000-8000-000000000001';
const P_ABIERTA = 'f0000000-0000-4000-8000-000000000002';
// Alumno que Identidad no devuelve: la pantalla muestra un nombre de reemplazo
const ALUMNO_SIN_NOMBRE_ID = '11111111-0000-0000-0000-000000000099';

const alumnos: UsuarioResumen[] = [
  { id: ALUMNO_1_A_ID, nombre: 'Ana', apellido: 'Pérez', email: 'alumno1@a.test', rol: 'Alumno' },
  { id: ALUMNO_2_A_ID, nombre: 'Bruno', apellido: 'Gómez', email: 'alumno2@a.test', rol: 'Alumno' },
];

const entregas: EntregaDetalle[] = [
  {
    entregaId: 'e1000000-0000-4000-8000-000000000001',
    alumnoId: ALUMNO_1_A_ID,
    salaId: SALA_ID,
    estado: 'Corregida',
    respondioAlgo: true,
    notaMultipleChoice: 2,
    puntajeMaximoMultipleChoice: 2,
    notaSugeridaAbiertas: 6.5,
    puntajeMaximoAbiertas: 8,
    fechaEntregaUtc: '2026-11-05T14:35:00Z',
    alertasFoco: { amarillas: 2, rojas: 1 },
    respuestas: [
      {
        preguntaId: P_MC,
        enunciado: '¿Qué imprime un bucle for que va de 1 a 3 sumando 1 en cada vuelta?',
        tipo: 'OpcionMultiple',
        puntajeMaximo: 2,
        respuesta: 'a0000000-0000-4000-8000-000000000001',
        opcionElegidaTexto: '1 2 3',
        esCorrecta: true,
        puntajeObtenido: 2,
        estadoCorreccion: 'Automatica',
      },
      {
        preguntaId: P_ABIERTA,
        enunciado: 'Explicá la diferencia entre una variable y una constante.',
        tipo: 'Abierta',
        puntajeMaximo: 8,
        respuesta: 'Una variable puede cambiar su valor durante la ejecución; una constante no.',
        notaSugerida: 6.5,
        justificacionIA:
          'Define bien ambos conceptos, pero no da un ejemplo de cada una, que la rúbrica pedía.',
        desglosePorCriterio: [
          { criterio: 'Define correctamente variable', puntaje: 3, comentario: 'Correcto.' },
          { criterio: 'Define correctamente constante', puntaje: 3, comentario: 'Correcto.' },
          { criterio: 'Da un ejemplo de cada una', puntaje: 0.5, comentario: 'No incluye ejemplos.' },
        ],
        estadoCorreccion: 'SugeridaIA',
      },
    ],
    alertas: [
      { preguntaId: P_MC, tipoEvento: 'visibilitychange', duracionMs: 1800, severidad: 'Amarillo', registradoUtc: '2026-11-05T14:12:00Z' },
      { preguntaId: P_MC, tipoEvento: 'blur', duracionMs: 900, severidad: 'Amarillo', registradoUtc: '2026-11-05T14:14:00Z' },
      { preguntaId: P_ABIERTA, tipoEvento: 'visibilitychange', duracionMs: 65000, severidad: 'Roja', registradoUtc: '2026-11-05T14:25:00Z' },
    ],
  },
  {
    entregaId: 'e1000000-0000-4000-8000-000000000002',
    alumnoId: ALUMNO_2_A_ID,
    salaId: SALA_ID,
    estado: 'PendienteCorreccion',
    respondioAlgo: true,
    notaMultipleChoice: 0,
    puntajeMaximoMultipleChoice: 2,
    notaSugeridaAbiertas: null,
    puntajeMaximoAbiertas: 8,
    fechaEntregaUtc: '2026-11-05T14:36:00Z',
    alertasFoco: { amarillas: 0, rojas: 0 },
    respuestas: [
      {
        preguntaId: P_MC,
        enunciado: '¿Qué imprime un bucle for que va de 1 a 3 sumando 1 en cada vuelta?',
        tipo: 'OpcionMultiple',
        puntajeMaximo: 2,
        respuesta: 'a0000000-0000-4000-8000-000000000002',
        opcionElegidaTexto: '0 1 2',
        esCorrecta: false,
        puntajeObtenido: 0,
        estadoCorreccion: 'Automatica',
      },
      {
        preguntaId: P_ABIERTA,
        enunciado: 'Explicá la diferencia entre una variable y una constante.',
        tipo: 'Abierta',
        puntajeMaximo: 8,
        respuesta: 'Una constante es una variable que no cambia.',
        notaSugerida: null,
        justificacionIA: null,
        estadoCorreccion: 'PendienteIA',
      },
    ],
    alertas: [],
  },
  {
    entregaId: 'e1000000-0000-4000-8000-000000000003',
    alumnoId: ALUMNO_SIN_NOMBRE_ID,
    salaId: SALA_ID,
    estado: 'Corregida',
    respondioAlgo: false,
    notaMultipleChoice: 0,
    puntajeMaximoMultipleChoice: 2,
    notaSugeridaAbiertas: 0,
    puntajeMaximoAbiertas: 8,
    fechaEntregaUtc: '2026-11-05T14:40:00Z',
    alertasFoco: { amarillas: 0, rojas: 0 },
    respuestas: [
      {
        preguntaId: P_MC,
        enunciado: '¿Qué imprime un bucle for que va de 1 a 3 sumando 1 en cada vuelta?',
        tipo: 'OpcionMultiple',
        puntajeMaximo: 2,
        respuesta: null,
        estadoCorreccion: 'SinRespuesta',
      },
      {
        preguntaId: P_ABIERTA,
        enunciado: 'Explicá la diferencia entre una variable y una constante.',
        tipo: 'Abierta',
        puntajeMaximo: 8,
        respuesta: null,
        estadoCorreccion: 'SinRespuesta',
      },
    ],
    alertas: [],
  },
];

// El listado trae lo mismo que el detalle, menos las respuestas y las alertas
function resumen(entrega: EntregaDetalle): EntregaResumen {
  const copia: Partial<EntregaDetalle> = { ...entrega };
  delete copia.respuestas;
  delete copia.alertas;
  return copia as EntregaResumen;
}

const sinInstitucion = () =>
  HttpResponse.json({ title: 'Petición inválida', status: 400, detail: 'Falta la institución.' }, { status: 400 });

export const handlersEntregas = [
  http.get('*/api/v1/evaluaciones/:id/entregas', ({ params, request }) => {
    if (!request.headers.get('X-Institucion-Id')) return sinInstitucion();
    // Solo el primer examen del mock tiene entregas
    return HttpResponse.json(params.id === EVALUACION_ID ? entregas.map(resumen) : []);
  }),

  http.get('*/api/v1/evaluaciones/:id/entregas/:entregaId', ({ params, request }) => {
    if (!request.headers.get('X-Institucion-Id')) return sinInstitucion();
    const entrega = params.id === EVALUACION_ID ? entregas.find((e) => e.entregaId === params.entregaId) : undefined;
    return entrega
      ? HttpResponse.json(entrega)
      : HttpResponse.json({ title: 'Entrega no encontrada.', status: 404, errores: [] }, { status: 404 });
  }),

  // Por el gateway llega a Identidad como GET /api/usuarios?rol=Alumno
  http.get('*/api/v1/identidad/usuarios', ({ request }) => {
    const rol = new URL(request.url).searchParams.get('rol');
    return HttpResponse.json(rol && rol !== 'Alumno' ? [] : alumnos);
  }),
];
