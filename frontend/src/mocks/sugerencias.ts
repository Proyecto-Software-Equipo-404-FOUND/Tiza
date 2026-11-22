import { delay, http, HttpResponse } from 'msw';

type Tipo = 'OpcionMultiple' | 'Abierta';

type Pedido = { materialId?: string; cantidad?: number; tipos?: Tipo[]; tema?: string | null };

const problema = (status: number, title: string, type?: string) =>
  HttpResponse.json(type ? { title, status, type } : { title, status }, { status });

// Preguntas de ejemplo (el mock no lee el apunte: inventa contenido para probar la pantalla)
function armarPregunta(indice: number, tipo: Tipo) {
  const numero = indice + 1;
  if (tipo === 'OpcionMultiple') {
    return {
      enunciado: `Pregunta de ejemplo ${numero}: ¿qué hace una estructura condicional?`,
      tipo,
      puntajeMaximo: 1,
      opciones: [
        { texto: 'Decide qué instrucciones se ejecutan según una condición', esCorrecta: true },
        { texto: 'Repite instrucciones sin límite', esCorrecta: false },
        { texto: 'Declara una variable', esCorrecta: false },
      ],
      fragmentoApunte: 'Las estructuras de control permiten decidir qué instrucciones se ejecutan y cuántas veces.',
    };
  }
  // Una de cada tres abiertas viene sin rúbrica y sin fragmento, para probar esos casos
  const completa = numero % 3 !== 0;
  return {
    enunciado: `Pregunta de ejemplo ${numero}: explicá con tus palabras para qué sirve un bucle.`,
    tipo,
    puntajeMaximo: 2,
    ...(completa
      ? {
          rubrica: {
            criterios: [
              { descripcion: 'Explica que repite instrucciones', puntaje: 1 },
              { descripcion: 'Da un ejemplo', puntaje: 1 },
            ],
          },
        }
      : {}),
    fragmentoApunte: completa ? 'Un bucle ejecuta un bloque de instrucciones varias veces.' : null,
  };
}

export const handlersSugerencias = [
  // S3-IA-02 (#241). Trucos según el tema:
  //  - "prohibido"  -> 403
  //  - "sin texto"  -> 422 MATERIAL_SIN_TEXTO
  //  - "no existe"  -> 404 MATERIAL_NO_ENCONTRADO
  //  - "ia caida"   -> 503 IA_NO_DISPONIBLE
  //  - "vacio"      -> 200 sin preguntas
  http.post('*/api/v1/sugerencias-preguntas', async ({ request }) => {
    const { materialId, cantidad = 5, tipos = ['OpcionMultiple', 'Abierta'], tema } = (await request.json()) as Pedido;
    await delay(1500);

    if (!materialId) return problema(400, 'Falta el apunte.');
    if (!Number.isInteger(cantidad) || cantidad < 1 || cantidad > 10) {
      return problema(400, 'La cantidad tiene que estar entre 1 y 10.');
    }
    if (tipos.length === 0) return problema(400, 'Elegí al menos un tipo de pregunta.');

    const clave = (tema ?? '').toLowerCase();
    if (clave.includes('prohibido')) return problema(403, 'Solo los docentes pueden pedir sugerencias.');
    if (clave.includes('no existe')) return problema(404, 'Material no encontrado.', 'MATERIAL_NO_ENCONTRADO');
    if (clave.includes('sin texto')) {
      return problema(422, 'El apunte no tiene texto para generar preguntas.', 'MATERIAL_SIN_TEXTO');
    }
    if (clave.includes('ia caida')) {
      return problema(503, 'La IA no está disponible. Probá de nuevo en unos minutos.', 'IA_NO_DISPONIBLE');
    }

    const preguntas = clave.includes('vacio')
      ? []
      : Array.from({ length: cantidad }, (_, indice) => armarPregunta(indice, tipos[indice % tipos.length]));

    return HttpResponse.json({
      materialId,
      preguntas,
      proveedor: 'mock',
      modelo: 'mock-1',
      revisadaPorAgenteRevisor: true,
    });
  }),
];
