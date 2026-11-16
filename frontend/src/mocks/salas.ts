import { http, HttpResponse } from 'msw';
import { evaluaciones } from './evaluaciones';

// Datos falsos con la forma de contracts/openapi/sala-en-vivo-v1.json (POST /api/v1/salas)
const CAPACIDAD_MINIMA = 1;
const CAPACIDAD_MAXIMA = 100;
const CAPACIDAD_POR_DEFECTO = 40;
const VIGENCIA_MS = 2 * 60 * 60 * 1000; // la sala vence a las 2 h

// PIN entero de 6 cifras (100000–999999), como el contrato
function generarPin(): number {
  return Math.floor(100000 + Math.random() * 900000);
}

const peticionInvalida = (title: string) =>
  HttpResponse.json({ title, status: 400 }, { status: 400 });

export const handlersSalas = [
  // 201 → { id, codigoPIN, fechaVencimiento, capacidadMaxima }.
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

    return HttpResponse.json(
      {
        id: crypto.randomUUID(),
        codigoPIN: generarPin(),
        fechaVencimiento: new Date(
          Date.now() + (cuerpo.duracionMinutos ? cuerpo.duracionMinutos * 60_000 : VIGENCIA_MS),
        ).toISOString(),
        capacidadMaxima: capacidad,
      },
      { status: 201 },
    );
  }),
];