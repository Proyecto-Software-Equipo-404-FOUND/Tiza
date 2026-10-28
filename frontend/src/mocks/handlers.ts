import { http, HttpResponse } from 'msw';
import { usuarios, cursos } from './datos';
import { crearTokenFalso } from './token';
import { handlersMatriculas } from './matriculas';
import { handlersCohortes } from './cohortes';
import { handlersEvaluaciones } from './evaluaciones';

export const handlers = [
  // Ruta real por el gateway: el transform la entrega a Identidad como /api/auth/login
  http.post('*/api/v1/identidad/auth/login', async ({ request }) => {
    const { email } = (await request.json()) as { email: string; password: string };
    const usuario = usuarios.find((u) => u.email === email);
    if (!usuario) {
      return HttpResponse.json({ title: 'Credenciales inválidas', status: 401 }, { status: 401 });
    }
    const ahora = Date.now();
    return HttpResponse.json({
      accessToken: crearTokenFalso(usuario),
      refreshToken: crypto.randomUUID(),
      tokenType: 'Bearer',
      accessTokenExpiresAt: new Date(ahora + 15 * 60 * 1000).toISOString(),
      refreshTokenExpiresAt: new Date(ahora + 7 * 24 * 60 * 60 * 1000).toISOString(),
    });
  }),

  // Renovación: devuelve un accessToken nuevo y rota el refreshToken (como el login).
  // El mock no recuerda a quién pertenece cada refreshToken: usa el docente A.
  http.post('*/api/v1/identidad/auth/refresh', async ({ request }) => {
    const { refreshToken } = (await request.json()) as { refreshToken?: string };
    if (!refreshToken) {
      return HttpResponse.json({ title: 'Token inválido', status: 401 }, { status: 401 });
    }
    const ahora = Date.now();
    return HttpResponse.json({
      accessToken: crearTokenFalso(usuarios[1]),
      refreshToken: crypto.randomUUID(),
      tokenType: 'Bearer',
      accessTokenExpiresAt: new Date(ahora + 15 * 60 * 1000).toISOString(),
      refreshTokenExpiresAt: new Date(ahora + 7 * 24 * 60 * 60 * 1000).toISOString(),
    });
  }),

  // Cerrar sesión: el backend real responde 204 sin contenido
  http.post('*/api/v1/identidad/auth/logout', () => new HttpResponse(null, { status: 204 })),

  // El backend real devuelve un objeto paginado (10 por página por defecto)
  http.get('*/api/v1/cursos', ({ request }) => {
    const url = new URL(request.url);
    const pagina = Number(url.searchParams.get('pagina') ?? 1);
    const tamanoPagina = Number(url.searchParams.get('tamanoPagina') ?? 10);
    const inicio = (pagina - 1) * tamanoPagina;
    return HttpResponse.json({
      items: cursos.slice(inicio, inicio + tamanoPagina),
      totalItems: cursos.length,
      pagina,
      tamanoPagina,
      totalPaginas: Math.max(1, Math.ceil(cursos.length / tamanoPagina)),
    });
  }),

  http.get('*/api/v1/cursos/:id', ({ params }) => {
    const curso = cursos.find((c) => c.id === params.id);
    return curso
      ? HttpResponse.json(curso)
      : HttpResponse.json({ error: 'Curso no encontrado' }, { status: 404 });
  }),

  http.post('*/api/v1/cursos', async ({ request }) => {
    const { nombre, area } = (await request.json()) as { nombre: string; area: string };

    // Truco para probar cómo se ve un conflicto en la pantalla
    if (nombre.toLowerCase() === 'conflicto') {
      return HttpResponse.json({ title: 'Conflicto', status: 409 }, { status: 409 });
    }
    const nuevo = {
      id: crypto.randomUUID(),
      institucionId: cursos[0].institucionId,
      nombre,
      area,
      activo: true,
      fechaCreacionUtc: new Date().toISOString(),
    };
    cursos.push(nuevo);
    return HttpResponse.json(nuevo, {
      status: 201,
      headers: { Location: `/api/v1/cursos/${nuevo.id}` },
    });
  }),
  ...handlersMatriculas,
  ...handlersCohortes,
  ...handlersEvaluaciones,
];