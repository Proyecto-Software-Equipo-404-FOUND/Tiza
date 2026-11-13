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
  estado: 'Activa' | 'Baja';
}> = [];

export const handlersMatriculas = [
  // GET /api/usuarios (S2-ID-10) por el gateway; acá solo se atiende el filtro rol=Alumno
  http.get('*/api/v1/identidad/usuarios', ({ request }) => {
    const rol = new URL(request.url).searchParams.get('rol');
    return HttpResponse.json(rol && rol !== 'Alumno' ? [] : alumnos);
  }),

  // POST /api/auth/register por el gateway (solo Administrador crea alumnos). Email repetido → 409.
  http.post('*/api/v1/identidad/auth/register', async ({ request }) => {
    const { nombre, apellido, email, rol } = (await request.json()) as {
      nombre: string;
      apellido: string;
      email: string;
      rol: string;
    };
    if (usuarios.some((u) => u.email === email) || alumnos.some((a) => a.email === email)) {
      return HttpResponse.json({ title: 'Conflicto', status: 409, detail: 'Ya existe un usuario con ese email.' }, { status: 409 });
    }
    const id = crypto.randomUUID();
    // Todos entran a la base de usuarios (es la que usa el login del mock, así el nuevo usuario puede ingresar);
    // además los alumnos van a la lista de matrícula y los docentes los atiende rol=Docente.
    usuarios.push({
      id,
      nombre: `${nombre} ${apellido}`,
      email,
      rol: rol === 'Alumno' ? 'alumno' : 'docente',
      institucionId: INSTITUCION_A,
    });
    if (rol === 'Alumno') alumnos.push({ id, nombre, apellido, email, rol: 'Alumno' });
    return HttpResponse.json({ id, message: 'Usuario registrado correctamente.' }, { status: 201 });
  }),

  // Como el servidor: por defecto solo las activas; con soloActivas=false también las dadas de baja
  http.get('*/api/v1/cohortes/:cohorteId/matriculas', ({ params, request }) => {
    const soloActivas = new URL(request.url).searchParams.get('soloActivas') !== 'false';
    return HttpResponse.json(
      matriculas.filter((m) => m.cohorteId === params.cohorteId && (!soloActivas || m.estado === 'Activa')),
    );
  }),

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

  // Baja lógica como el servidor: 204, la matrícula queda en estado Baja; si ya estaba de baja, 409
  http.delete('*/api/v1/cohortes/:cohorteId/matriculas/:id', ({ params }) => {
    const matricula = matriculas.find((m) => m.cohorteId === params.cohorteId && m.id === params.id);
    if (!matricula) return HttpResponse.json({ title: 'Matrícula no encontrada', status: 404 }, { status: 404 });
    if (matricula.estado === 'Baja') {
      return HttpResponse.json(
        { title: 'Conflicto', status: 409, detail: 'La matriculación ya se encuentra dada de baja.' },
        { status: 409 },
      );
    }
    matricula.estado = 'Baja';
    return new HttpResponse(null, { status: 204 });
  }),

  http.post('*/api/v1/cohortes/:cohorteId/matriculas/:id/reactivar', ({ params }) => {
    const matricula = matriculas.find((m) => m.cohorteId === params.cohorteId && m.id === params.id);
    if (!matricula) return HttpResponse.json({ title: 'Matrícula no encontrada', status: 404 }, { status: 404 });
    if (matricula.estado === 'Activa') {
      return HttpResponse.json({ title: 'Conflicto', status: 409, detail: 'La matriculación ya se encuentra activa.' }, { status: 409 });
    }
    matricula.estado = 'Activa';
    return new HttpResponse(null, { status: 204 });
  }),
];
