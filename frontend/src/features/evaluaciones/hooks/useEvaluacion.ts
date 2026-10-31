import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { obtenerEvaluacion } from '../api/evaluaciones';

export function useEvaluacion(id: string) {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useQuery({
    queryKey: ['evaluaciones', institucionId, id],
    queryFn: () => obtenerEvaluacion(id, institucionId),
    enabled: institucionId !== '' && id !== '',
  });
}
