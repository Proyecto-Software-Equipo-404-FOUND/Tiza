import { http, HttpResponse } from 'msw';
import type { Cohorte } from '@/shared/api/tipos';
import { cursos } from './datos';

const cohortes: Cohorte[] = [
  { id: 'co-1', cursoId: 'c-1', nombre: 'Cohorte 2026 - Mañana', docenteId: 'u-docente-a' },
];

export const handlersCohortes = [
  http.get('*/api/v1/cursos/:cursoId/cohortes', ({ params }) => {
    if (!cursos.some((c) => c.id === params.cursoId)) {
      return HttpResponse.json({ title: 'Curso no encontrado', status: 404 }, { status: 404 });
    }
    return HttpResponse.json(cohortes.filter((c) => c.cursoId === params.cursoId));
  }),

  http.post('*/api/v1/cursos/:cursoId/cohortes', async ({ params, request }) => {
    const cursoId = String(params.cursoId);
    if (!cursos.some((c) => c.id === cursoId)) {
      return HttpResponse.json({ title: 'Curso no encontrado', status: 404 }, { status: 404 });
    }
    const { nombre } = (await request.json()) as { nombre: string };
    // Mismo truco que en cursos: un nombre 'conflicto' simula el 409
    if (nombre.toLowerCase() === 'conflicto') {
      return HttpResponse.json({ title: 'Conflicto', status: 409 }, { status: 409 });
    }
    const nueva: Cohorte = { id: crypto.randomUUID(), cursoId, nombre, docenteId: 'u-docente-a' };
    cohortes.push(nueva);
    return HttpResponse.json(nueva, {
      status: 201,
      headers: { Location: `/api/v1/cursos/${cursoId}/cohortes/${nueva.id}` },
    });
  }),
];