import { http, HttpResponse } from 'msw';
import { usuarios } from './datos';

// GET /api/usuarios (S2-ID-10) por el gateway. Acá solo se atiende rol=Docente; cualquier otro rol
// sigue al handler siguiente (matrículas y resultados atienden rol=Alumno), por eso va primero.
export const handlersUsuarios = [
  http.get('*/api/v1/identidad/usuarios', ({ request }) => {
    if (new URL(request.url).searchParams.get('rol') !== 'Docente') return undefined;
    const docentes = usuarios
      .filter((u) => u.rol === 'docente')
      .map(({ id, nombre, email }) => {
        // «Docente A» → nombre «Docente», apellido «A», como UsuarioResumen del contrato de Identidad
        const corte = nombre.lastIndexOf(' ');
        return { id, nombre: nombre.slice(0, corte), apellido: nombre.slice(corte + 1), email, rol: 'Docente' };
      });
    return HttpResponse.json(docentes);
  }),
];
