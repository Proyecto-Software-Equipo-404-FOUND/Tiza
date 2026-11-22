import { http, HttpResponse } from 'msw';
import type { MetricasEvaluacion } from '@/features/metricas/tipos';
import { ALUMNO_1_A_ID, ALUMNO_2_A_ID } from './datos';
import { evaluaciones } from './evaluaciones';
import { rolDelToken } from './token';

// Datos falsos con la forma de MetricasEvaluacion (contracts/openapi/evaluaciones-v1.json, S3-EV-05).
// Solo cuentan las notas aprobadas. El tercer alumno no existe en Identidad: la pantalla muestra un nombre de reemplazo.
const ALUMNO_SIN_NOMBRE_ID = '11111111-0000-0000-0000-000000000099';
const redondear = (valor: number) => Math.round(valor * 100) / 100;

function metricasDelExamen(evaluacionId: string): MetricasEvaluacion | null {
  const examen = evaluaciones.find((e) => e.id === evaluacionId);
  if (!examen) return null;

  // Solo el primer examen de las semillas tiene entregas: los demás muestran «todavía no hay entregas»
  if (examen !== evaluaciones[0]) {
    return { evaluacionId, cantidadEntregas: 0, cantidadAprobadas: 0, puntajeMaximo: 10, promedio: null, preguntaMasDificilId: null, preguntas: [], alumnos: [] };
  }

  const preguntas = examen.preguntasJson.preguntas;
  const puntajeMaximo = preguntas.reduce((suma, p) => suma + p.puntajeMaximo, 0);
  // Cuánto sacó cada alumno en cada pregunta (en el orden de las preguntas del examen)
  const obtenidos = [
    { alumnoId: ALUMNO_1_A_ID, porPregunta: [2, 6.5] },
    { alumnoId: ALUMNO_2_A_ID, porPregunta: [2, 3] },
    { alumnoId: ALUMNO_SIN_NOMBRE_ID, porPregunta: [0, 3.5] },
  ];
  const notas = obtenidos.map((o) => ({ alumnoId: o.alumnoId, notaFinal: o.porPregunta.reduce((a, b) => a + b, 0) }));
  const promedio = redondear(notas.reduce((suma, n) => suma + n.notaFinal, 0) / notas.length);

  const metricasPreguntas = preguntas.map((p, i) => {
    const promedioPregunta = redondear(obtenidos.reduce((suma, o) => suma + o.porPregunta[i], 0) / obtenidos.length);
    return {
      preguntaId: p.id,
      enunciado: p.enunciado,
      puntajeMaximo: p.puntajeMaximo,
      promedio: promedioPregunta,
      porcentaje: redondear((promedioPregunta / p.puntajeMaximo) * 100),
    };
  });
  const masDificil = [...metricasPreguntas].sort((a, b) => a.porcentaje - b.porcentaje)[0];

  return {
    evaluacionId,
    cantidadEntregas: 4,
    cantidadAprobadas: notas.length,
    puntajeMaximo,
    promedio,
    preguntaMasDificilId: masDificil?.preguntaId ?? null,
    preguntas: metricasPreguntas,
    alumnos: notas.map((n) => ({ ...n, diferenciaConPromedio: redondear(n.notaFinal - promedio) })),
  };
}

export const handlersMetricas = [
  // 200 → MetricasEvaluacion. Errores como el contrato: 401 sin token, 403 si no es docente ni administrador, 404 examen inexistente
  http.get('*/api/v1/evaluaciones/:id/metricas', ({ params, request }) => {
    const rol = rolDelToken(request);
    if (!rol) return HttpResponse.json({ title: 'No autenticado.', status: 401 }, { status: 401 });
    if (rol !== 'Docente' && rol !== 'Administrador') {
      return HttpResponse.json({ title: 'El rol no puede ver métricas.', status: 403 }, { status: 403 });
    }
    const metricas = metricasDelExamen(String(params.id));
    if (!metricas) return HttpResponse.json({ title: 'La evaluación no existe.', status: 404 }, { status: 404 });
    return HttpResponse.json(metricas);
  }),
];
