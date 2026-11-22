import { http, HttpResponse } from 'msw';
import { usuarios } from './datos';

// GET /api/usuarios (S2-ID-10) por el gateway. Acá solo se atiende rol=Docente; cualquier otro rol
// sigue al handler siguiente (matrículas y resultados atienden rol=Alumno), por eso va primero.
export const handlersUsuarios = [
  // POST /api/usuarios/alumnos/importacion (S2-ID-02) por el gateway. Lee nombre,apellido,email y devuelve
  // el resumen: email repetido → omitido, fila inválida → error con su número (la fila 1 son los títulos).
  http.post('*/api/v1/identidad/usuarios/alumnos/importacion', async ({ request }) => {
    const archivo = (await request.formData()).get('archivo');
    if (!(archivo instanceof File)) {
      return HttpResponse.json({ title: 'Falta el archivo.', status: 400 }, { status: 400 });
    }
    const filas = (await archivo.text())
      .replace(/^\uFEFF/, '')
      .split(/\r?\n/)
      .map((linea, i) => ({ fila: i + 1, columnas: linea.split(',').map((c) => c.trim()) }))
      .filter(({ fila, columnas }) => fila > 1 && columnas.some((c) => c !== ''));
    if (filas.length === 0 || filas.length > 200) {
      const detail = filas.length === 0 ? 'El archivo no tiene alumnos.' : 'El archivo supera los 200 alumnos.';
      return HttpResponse.json({ title: 'Archivo inválido.', detail, status: 400 }, { status: 400 });
    }

    const vistos = new Set(usuarios.map((u) => u.email.toLowerCase()));
    const errores: { fila: number; motivo: string }[] = [];
    let creados = 0;
    let omitidos = 0;
    for (const { fila, columnas } of filas) {
      const [nombre, apellido, email] = columnas;
      if (!nombre || !apellido) errores.push({ fila, motivo: 'Faltan el nombre o el apellido' });
      else if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errores.push({ fila, motivo: 'Email inválido' });
      else if (vistos.has(email.toLowerCase())) omitidos++;
      else {
        vistos.add(email.toLowerCase());
        creados++;
      }
    }
    return HttpResponse.json({ creados, omitidos, errores });
  }),

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
