import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { eliminarEvaluacion } from '../api/evaluaciones';

export function useEliminarEvaluacion(id: string) {
  const cliente = useQueryClient();
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useMutation({
    mutationFn: () => eliminarEvaluacion(id, institucionId),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['evaluaciones'] }),
  });
}
