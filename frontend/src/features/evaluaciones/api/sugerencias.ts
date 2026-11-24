import { pedir } from '@/shared/api/cliente';
import type { Curso, ResultadoPaginado } from '@/shared/api/tipos';
import type { ApunteSugerible, DatosSugerencia, RespuestaSugerencias } from '../sugerencias';

// El contrato (ia-v1.json) fija 75 s para el front: la IA responde en 60 s como máximo
export const TIMEOUT_SUGERENCIAS_MS = 75_000;

export async function sugerirPreguntas(datos: DatosSugerencia): Promise<RespuestaSugerencias> {
  const controlador = new AbortController();
  const temporizador = setTimeout(() => controlador.abort(), TIMEOUT_SUGERENCIAS_MS);
  try {
    return await pedir<RespuestaSugerencias>('/api/v1/sugerencias-preguntas', {
      method: 'POST',
      body: JSON.stringify(datos),
      signal: controlador.signal,
    });
  } finally {
    clearTimeout(temporizador);
  }
}

// Mismas rutas que usa la feature de cursos; se repiten acá porque una feature no importa de otra
export const listarCursosParaSugerir = () => pedir<ResultadoPaginado<Curso>>('/api/v1/cursos');

export const listarApuntesParaSugerir = (cursoId: string) =>
  pedir<ApunteSugerible[]>(`/api/v1/cursos/${cursoId}/materiales`);
