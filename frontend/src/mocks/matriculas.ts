import { http, HttpResponse } from 'msw';
import { INSTITUCION_A, usuarios } from './datos';

// Datos falsos de matrícula con la forma de los contratos de Cursos e Identidad.
// Los alumnos salen de datos.ts, así los ids coinciden con las semillas de Identidad (ID-08).
const alumnos = usuarios
  .filter((u) => u.rol === 'alumno')
  .map(({ id, nombre, email }) => {
    // «Alumno 1 A» → nombre «Alumno 1», apellido «A», como UsuarioResumen del contrato de Identidad
    const corte = nombre.lastIndexOf(' ');
    return { id, nombre: nombre.slice(0, corte), apellido: nombre.slice(corte + 1), email, rol: 'Alumno' };
  });

// Con la forma de MatriculacionDto del contrato de Cursos: trae el id del alumno pero no su nombre
const matriculas: Array<{
  id: string;
  institucionId: string;
  cohorteId: string;
  alumnoId: string;
  fechaAlta: string;
  estado: 'Activa';
}> = [];

export const handlersMatriculas = [
  // GET /api/usuarios (S2-ID-10) por el gateway; acá solo se atiende el filtro rol=Alumno
  http.get('*/api/v1/identidad/usuarios', ({ request }) => {
    const rol = new URL(request.url).searchParams.get('rol');
    return HttpResponse.json(rol && rol !== 'Alumno' ? [] : alumnos);
  }),

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
      institucionId: INSTITUCION_A,
      cohorteId,
      alumnoId,
      fechaAlta: new Date().toISOString(),
      estado: 'Activa' as const,
    };
    matriculas.push(nueva);
    return HttpResponse.json(nueva, {
      status: 201,
      headers: { Location: `/api/v1/cohortes/${cohorteId}/matriculas/${nueva.id}` },
    });
  }),
];
