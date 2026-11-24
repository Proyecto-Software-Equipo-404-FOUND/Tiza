import { useMutation } from '@tanstack/react-query';
import { sugerirPreguntas } from '../api/sugerencias';
import type { DatosSugerencia } from '../sugerencias';

// No invalida nada: las sugerencias no se guardan, solo se guardan las preguntas que el docente agrega
export function useSugerirPreguntas() {
  return useMutation({
    mutationFn: (datos: DatosSugerencia) => sugerirPreguntas(datos),
  });
}
