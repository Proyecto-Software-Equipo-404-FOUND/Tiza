import { http, HttpResponse } from 'msw';
import { usuarios } from './datos';

// Datos falsos de matrícula. Provisorio hasta que exista CU-04.
// Los alumnos salen de datos.ts, así los ids coinciden con las semillas de Identidad (ID-08).
const alumnos = usuarios
  .filter((u) => u.rol === 'alumno')
  .map(({ id, nombre }) => ({ id, nombre }));

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
