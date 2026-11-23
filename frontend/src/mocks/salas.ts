import { http, HttpResponse } from 'msw';
import { evaluaciones } from './evaluaciones';

// Datos falsos con la forma de contracts/openapi/sala-en-vivo-v1.json (POST /api/v1/salas)
const CAPACIDAD_MINIMA = 1;
const CAPACIDAD_MAXIMA = 100;
const CAPACIDAD_POR_DEFECTO = 40;
export const DURACION_POR_DEFECTO_MINUTOS = 120; // lo que usa el servidor si no llega duracionMinutos

// Duración de cada sala creada (por id): la usa la conexión de demo del docente al iniciar la sala
export const duracionesDeSalas = new Map<string, number>();

// PIN entero de 6 cifras (100000–999999), como el contrato
function generarPin(): number {
  return Math.floor(100000 + Math.random() * 900000);
}

const peticionInvalida = (title: string) =>
  HttpResponse.json({ title, status: 400 }, { status: 400 });

export const handlersSalas = [
  // 201 → { id, codigoPIN, fechaVencimiento (null hasta que se inicia la sala), capacidadMaxima, duracionMinutos }.
  // Errores como el contrato: 400 (cuerpo inválido), 404 (evaluación inexistente o no publicada).
  http.post('*/api/v1/salas', async ({ request }) => {
    const cuerpo = (await request.json()) as { evaluacionId?: string; capacidadMaxima?: number; duracionMinutos?: number };
    if (!cuerpo.evaluacionId) return peticionInvalida('Falta la evaluación.');

    const capacidad = cuerpo.capacidadMaxima ?? CAPACIDAD_POR_DEFECTO;
    if (!Number.isInteger(capacidad) || capacidad < CAPACIDAD_MINIMA || capacidad > CAPACIDAD_MAXIMA) {
      return peticionInvalida(`La capacidad debe estar entre ${CAPACIDAD_MINIMA} y ${CAPACIDAD_MAXIMA}.`);
    }

    const evaluacion = evaluaciones.find((e) => e.id === cuerpo.evaluacionId);
    if (!evaluacion || evaluacion.estado !== 'Publicada') {
      return HttpResponse.json({ title: 'Evaluación no encontrada.', status: 404 }, { status: 404 });
    }

    const id = crypto.randomUUID();
    const duracionMinutos = cuerpo.duracionMinutos ?? DURACION_POR_DEFECTO_MINUTOS;
    duracionesDeSalas.set(id, duracionMinutos);

    return HttpResponse.json(
      {
        id,
        codigoPIN: generarPin(),
        fechaVencimiento: null,
        capacidadMaxima: capacidad,
        duracionMinutos,
      },
      { status: 201 },
    );
  }),
];