import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { crearEvaluacion } from '../api/evaluaciones';
import type { DatosEvaluacion } from '../borrador';

export function useCrearEvaluacion() {
  const cliente = useQueryClient();
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useMutation({
    mutationFn: (datos: DatosEvaluacion) => crearEvaluacion(institucionId, datos),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['evaluaciones'] }),
  });
}
