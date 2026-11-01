import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { actualizarEvaluacion } from '../api/evaluaciones';
import type { DatosEvaluacion } from '../borrador';

export function useActualizarEvaluacion(id: string) {
  const cliente = useQueryClient();
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useMutation({
    mutationFn: (datos: DatosEvaluacion) => actualizarEvaluacion(id, institucionId, datos),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['evaluaciones'] }),
  });
}
