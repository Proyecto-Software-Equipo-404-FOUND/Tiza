import { http, HttpResponse } from 'msw';
import type { Cohorte } from '@/shared/api/tipos';
import { cursos, CURSO_1_ID, COHORTE_1_ID, DOCENTE_A_ID } from './datos';

const cohortes: Cohorte[] = [
  {
    id: COHORTE_1_ID,
    cursoId: CURSO_1_ID,
    docenteId: DOCENTE_A_ID,
    nombre: 'Cohorte 2026 - Mañana',
    periodo: '2026-1C',
    activo: true,
    fechaCreacionUtc: '2026-10-09T12:00:00Z',
  },
];

export const handlersCohortes = [
  http.get('*/api/v1/cursos/:cursoId/cohortes', ({ params }) => {
    if (!cursos.some((c) => c.id === params.cursoId)) {
      return HttpResponse.json({ error: 'Curso no encontrado' }, { status: 404 });
    }
    return HttpResponse.json(cohortes.filter((c) => c.cursoId === params.cursoId));
  }),

  http.post('*/api/v1/cursos/:cursoId/cohortes', async ({ params, request }) => {
    const cursoId = String(params.cursoId);
    if (!cursos.some((c) => c.id === cursoId)) {
      return HttpResponse.json({ error: 'Curso no encontrado' }, { status: 404 });
    }
    const { nombre, periodo, docenteId } = (await request.json()) as {
      nombre: string;
      periodo: string;
      docenteId: string;
    };
    // Mismo truco que en cursos: un nombre 'conflicto' simula el 409
    if (nombre.toLowerCase() === 'conflicto') {
      return HttpResponse.json({ title: 'Conflicto', status: 409 }, { status: 409 });
    }
    const nueva: Cohorte = {
      id: crypto.randomUUID(),
      cursoId,
      docenteId,
      nombre,
      periodo,
      activo: true,
      fechaCreacionUtc: new Date().toISOString(),
    };
    cohortes.push(nueva);
    return HttpResponse.json(nueva, {
      status: 201,
      headers: { Location: `/api/v1/cursos/${cursoId}/cohortes/${nueva.id}` },
    });
  }),
];