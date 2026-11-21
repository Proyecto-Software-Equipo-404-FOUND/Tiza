import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { obtenerMetricas } from '../api/metricas';

// La institución forma parte de la clave: los datos de una no se reutilizan para otra
export function useMetricas(evaluacionId: string) {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useQuery({
    queryKey: ['metricas', institucionId, evaluacionId],
    queryFn: () => obtenerMetricas(evaluacionId),
    enabled: institucionId !== '' && evaluacionId !== '',
  });
}
