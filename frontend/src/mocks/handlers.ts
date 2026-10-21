import { http, HttpResponse } from 'msw';
import { usuarios, cursos } from './datos';
import { handlersMatriculas } from './matriculas';

export const handlers = [
  // Ruta provisional: va bajo el prefijo que el gateway reserva para Identidad
  http.post('*/api/v1/identidad/sesiones', async ({ request }) => {
    const { email } = (await request.json()) as { email: string; password: string };
    const usuario = usuarios.find((u) => u.email === email);
    if (!usuario) {
      return HttpResponse.json({ title: 'Credenciales inválidas', status: 401 }, { status: 401 });
    }
    return HttpResponse.json({ accessToken: `token-falso-${usuario.id}`, usuario });
  }),

  http.get('*/api/v1/cursos', () => HttpResponse.json(cursos)),

  http.post('*/api/v1/cursos', async ({ request }) => {
    const { nombre } = (await request.json()) as { nombre: string };

    // Truco para probar cómo se ve un conflicto en la pantalla
    if (nombre.toLowerCase() === 'conflicto') {
      return HttpResponse.json({ title: 'Conflicto', status: 409 }, { status: 409 });
    }
    const nuevo = { id: crypto.randomUUID(), nombre, institucionId: cursos[0].institucionId };
    cursos.push(nuevo);
    return HttpResponse.json(nuevo, {
      status: 201,
      headers: { Location: `/api/v1/cursos/${nuevo.id}` },
    });
  }),
  ...handlersMatriculas,
];
