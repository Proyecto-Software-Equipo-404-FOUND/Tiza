import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { listarEvaluaciones } from '../api/evaluaciones';

// La institución forma parte de la clave: los datos de una no se reutilizan para otra
export function useEvaluaciones() {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useQuery({
    queryKey: ['evaluaciones', institucionId],
    queryFn: () => listarEvaluaciones(institucionId),
    enabled: institucionId !== '',
  });
}
