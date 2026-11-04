import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { publicarEvaluacion } from '../api/evaluaciones';

export function usePublicarEvaluacion(id: string) {
  const cliente = useQueryClient();
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useMutation({
    mutationFn: (datos: { versionEsperada: number; cohorteId: string }) =>
      publicarEvaluacion(id, institucionId, datos),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['evaluaciones'] }),
  });
}
