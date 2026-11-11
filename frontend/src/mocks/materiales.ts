import { http, HttpResponse } from 'msw';
import type { Material } from '@/features/cursos/apuntes';
import { cursos, CURSO_1_ID } from './datos';

const DOCENTE_A_ID = '11111111-0000-0000-0000-000000000002';
const MAXIMO_BYTES = 10 * 1024 * 1024;

// Más nuevo primero, igual que el backend
const materiales: Material[] = [
  {
    id: 'dddddddd-0000-0000-0000-000000000002',
    cursoId: CURSO_1_ID,
    titulo: 'Unidad 2 - Estructuras de control',
    nombreArchivo: 'unidad-2.pdf',
    tamanoBytes: 842_000,
    cantidadPaginas: 12,
    estado: 'Procesando',
    subidoPorId: DOCENTE_A_ID,
    fechaSubidaUtc: '2026-11-06T15:30:00Z',
  },
  {
    id: 'dddddddd-0000-0000-0000-000000000001',
    cursoId: CURSO_1_ID,
    titulo: 'Unidad 1 - Introducción',
    nombreArchivo: 'unidad-1.pdf',
    tamanoBytes: 1_250_000,
    cantidadPaginas: 18,
    estado: 'Listo',
    subidoPorId: DOCENTE_A_ID,
    fechaSubidaUtc: '2026-10-20T12:00:00Z',
  },
];

const problema = (status: number, title: string) =>
  HttpResponse.json({ title, status }, { status });

export const handlersMateriales = [
  http.get('*/api/v1/cursos/:cursoId/materiales', ({ params }) => {
    if (!cursos.some((c) => c.id === params.cursoId)) {
      return problema(404, 'Curso no encontrado.');
    }
    return HttpResponse.json(materiales.filter((m) => m.cursoId === params.cursoId));
  }),

  http.post('*/api/v1/cursos/:cursoId/materiales', async ({ params, request }) => {
    const cursoId = String(params.cursoId);
    if (!cursos.some((c) => c.id === cursoId)) {
      return problema(404, 'Curso no encontrado.');
    }
    const datos = await request.formData();
    const archivo = datos.get('archivo');
    const titulo = String(datos.get('titulo') ?? '').trim();
    if (!(archivo instanceof File)) {
      return problema(400, 'Falta el archivo.');
    }

    // Trucos para forzar errores (según el título o el nombre del archivo):
    //  - "prohibido"  -> 403
    //  - sin ".pdf"   -> 415
    //  - "grande"     -> 400 por más de 30 páginas
    //  - "escaneado"  -> 400 por PDF sin texto
    const clave = (titulo || archivo.name).toLowerCase();
    if (clave.includes('prohibido')) {
      return problema(403, 'No tenés permiso para gestionar el material de este curso.');
    }
    if (!archivo.name.toLowerCase().endsWith('.pdf')) {
      return problema(415, 'Solo se aceptan archivos PDF.');
    }
    if (archivo.size > MAXIMO_BYTES) {
      return problema(400, 'El archivo supera el tamaño máximo de 10 MB.');
    }
    if (clave.includes('grande')) {
      return problema(400, 'El PDF supera el máximo de 30 páginas.');
    }
    if (clave.includes('escaneado')) {
      return problema(
        400,
        'El PDF no tiene texto seleccionable. Subí un PDF generado desde un procesador de texto, no escaneado.',
      );
    }

    const nuevo: Material = {
      id: crypto.randomUUID(),
      cursoId,
      titulo: titulo || archivo.name.replace(/\.[^.]+$/, ''),
      nombreArchivo: archivo.name,
      tamanoBytes: Math.max(1, archivo.size),
      cantidadPaginas: 1 + (archivo.size % 30),
      estado: 'Listo',
      subidoPorId: DOCENTE_A_ID,
      fechaSubidaUtc: new Date().toISOString(),
    };
    materiales.unshift(nuevo);
    return HttpResponse.json(nuevo, {
      status: 201,
      headers: { Location: `/api/v1/materiales/${nuevo.id}` },
    });
  }),

  // Baja lógica en el backend; el mock la quita de la lista
  http.delete('*/api/v1/cursos/:cursoId/materiales/:id', ({ params }) => {
    const indice = materiales.findIndex((m) => m.id === params.id && m.cursoId === params.cursoId);
    if (indice === -1) return problema(404, 'Material no encontrado.');
    materiales.splice(indice, 1);
    return new HttpResponse(null, { status: 204 });
  }),
];