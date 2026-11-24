import { http, HttpResponse } from 'msw';
import type { MiExamen } from '@/features/mis-examenes/tipos';
import { rolDelToken } from './token';

// Datos falsos con la forma de MiExamen (contracts/openapi/evaluaciones-v1.json), del más nuevo al más viejo
const examenes: MiExamen[] = [
  {
    evaluacionId: 'e0000000-0000-4000-8000-000000000001',
    entregaId: 'e1000000-0000-4000-8000-000000000001',
    titulo: 'Parcial 2 · Estructuras de control',
    fechaEntregaUtc: '2026-11-19T14:10:00Z',
    estado: 'PendienteCorreccion',
  },
  {
    evaluacionId: 'e0000000-0000-4000-8000-000000000002',
    entregaId: 'e1000000-0000-4000-8000-000000000002',
    titulo: 'Parcial 1 · Variables y tipos',
    fechaEntregaUtc: '2026-11-05T14:35:00Z',
    estado: 'Aprobada',
    notaFinal: 8.5,
    puntajeMaximo: 10,
  },
  {
    evaluacionId: 'e0000000-0000-4000-8000-000000000003',
    entregaId: 'e1000000-0000-4000-8000-000000000003',
    titulo: 'Quiz · Introducción',
    fechaEntregaUtc: '2026-10-22T13:00:00Z',
    estado: 'Aprobada',
    notaFinal: 4,
    puntajeMaximo: 5,
  },
];

export const handlersMisExamenes = [
  http.get('*/api/v1/alumnos/me/evaluaciones', ({ request }) => {
    const rol = rolDelToken(request);
    if (!rol) return HttpResponse.json({ title: 'No autenticado.', status: 401 }, { status: 401 });
    // Solo el alumno tiene «Mis exámenes»; Docente o Administrador → 403
    if (rol !== 'Alumno') {
      return HttpResponse.json({ title: 'Solo los alumnos tienen "Mis exámenes".', status: 403 }, { status: 403 });
    }
    return HttpResponse.json(examenes);
  }),
];
