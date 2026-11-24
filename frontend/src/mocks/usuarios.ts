import { http, HttpResponse } from 'msw';
import { usuarios } from './datos';

// GET /api/usuarios (S2-ID-10) por el gateway. Acá solo se atiende rol=Docente; cualquier otro rol
// sigue al handler siguiente (matrículas y resultados atienden rol=Alumno), por eso va primero.
export const handlersUsuarios = [
  // POST /api/usuarios/alumnos/importacion (S2-ID-02) por el gateway. Imita al backend real (CsvAlumnosParser):
  // el motivo de un 400 va en `title`, y un archivo mal armado se rechaza entero (encabezado distinto, una fila sin
  // tres columnas —también una línea en blanco—, más de 200 alumnos o de 1 MiB). Las filas con datos incorrectos
  // no frenan a las demás: email repetido → omitido, dato inválido → error con su número (la fila 1 son los títulos).
  http.post('*/api/v1/identidad/usuarios/alumnos/importacion', async ({ request }) => {
    const formulario = await request.formData();
    const archivo = formulario.get('archivo');
    const contrasenaInicial = formulario.get('contrasenaInicial');
    const rechazar = (title: string, type?: string) =>
      HttpResponse.json({ title, status: 400, ...(type ? { type } : {}) }, { status: 400 });

    if (!(archivo instanceof File)) return rechazar('Debe enviar el archivo en el campo archivo.');
    if (typeof contrasenaInicial === 'string' && contrasenaInicial !== '') {
      const invalida =
        contrasenaInicial.trim() === '' ||
        contrasenaInicial.length < 8 ||
        new TextEncoder().encode(contrasenaInicial).length > 72;
      if (invalida) {
        return rechazar(
          'La contraseña inicial tiene que tener al menos 8 caracteres (y no más de 72 bytes).',
          'CONTRASENA_INICIAL_INVALIDA',
        );
      }
    }
    if (archivo.size > 1024 * 1024) return rechazar('El archivo supera el máximo de 1 MiB.');
    if (archivo.size === 0) return rechazar('El archivo CSV está vacío.');

    const lineas = (await archivo.text()).replace(/^\uFEFF/, '').split(/\r?\n/);
    if (lineas[lineas.length - 1] === '') lineas.pop(); // un solo salto de línea al final es válido
    if (lineas[0] !== 'nombre,apellido,email') return rechazar('El encabezado debe ser exactamente nombre,apellido,email.');
    if (lineas.length - 1 > 200) return rechazar('El archivo supera el máximo de 200 filas de datos.');

    const filas = lineas.slice(1).map((linea, i) => ({ fila: i + 2, columnas: linea.split(',') }));
    const mal = filas.find(({ columnas }) => columnas.length !== 3);
    if (mal) return rechazar(`La fila CSV ${mal.fila} no contiene exactamente tres columnas.`);

    const vistos = new Set(usuarios.map((u) => u.email.toLowerCase()));
    const errores: { fila: number; motivo: string }[] = [];
    let creados = 0;
    let omitidos = 0;
    for (const { fila, columnas } of filas) {
      const [nombre, apellido, email] = columnas.map((c) => c.trim());
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
