import { http, HttpResponse } from 'msw';

// Datos falsos de matrícula. Provisorio hasta que exista CU-04.
const alumnos = [
  { id: 'u-alumno1-a', nombre: 'Alumno 1 A' },
  { id: 'u-alumno2-a', nombre: 'Alumno 2 A' },
  { id: 'u-alumno3-a', nombre: 'Alumno 3 A' },
];

const matriculas: Array<{
  id: string;
  cohorteId: string;
  alumnoId: string;
  alumnoNombre: string;
  estado: 'Activa';
}> = [];

export const handlersMatriculas = [
  http.get('*/api/v1/identidad/alumnos', () => HttpResponse.json(alumnos)),

  http.get('*/api/v1/cohortes/:cohorteId/matriculas', ({ params }) =>
    HttpResponse.json(matriculas.filter((m) => m.cohorteId === params.cohorteId)),
  ),

  http.post('*/api/v1/cohortes/:cohorteId/matriculas', async ({ params, request }) => {
    const { alumnoId } = (await request.json()) as { alumnoId: string };
    const cohorteId = String(params.cohorteId);
    const alumno = alumnos.find((a) => a.id === alumnoId);
    if (!alumno) {
      return HttpResponse.json({ title: 'Alumno inexistente', status: 404 }, { status: 404 });
    }
    // Para probar el conflicto: matricular dos veces al mismo alumno devuelve 409
    if (matriculas.some((m) => m.cohorteId === cohorteId && m.alumnoId === alumnoId)) {
      return HttpResponse.json({ title: 'Conflicto', status: 409 }, { status: 409 });
    }
    const nueva = {
      id: crypto.randomUUID(),
      cohorteId,
      alumnoId,
      alumnoNombre: alumno.nombre,
      estado: 'Activa' as const,
    };
    matriculas.push(nueva);
    return HttpResponse.json(nueva, {
      status: 201,
      headers: { Location: `/api/v1/cohortes/${cohorteId}/matriculas/${nueva.id}` },
    });
  }),
];
