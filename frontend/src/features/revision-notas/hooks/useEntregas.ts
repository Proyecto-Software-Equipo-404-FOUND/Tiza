import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { listarEntregas, obtenerEntrega } from '../api/entregas';

// La institución forma parte de la clave: los datos de una no se reutilizan para otra
export function useEntregas(evaluacionId: string) {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useQuery({
    queryKey: ['entregas', institucionId, evaluacionId],
    queryFn: () => listarEntregas(evaluacionId, institucionId),
    enabled: institucionId !== '' && evaluacionId !== '',
  });
}

export function useEntrega(evaluacionId: string, entregaId: string) {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  return useQuery({
    queryKey: ['entregas', institucionId, evaluacionId, entregaId],
    queryFn: () => obtenerEntrega(evaluacionId, entregaId, institucionId),
    enabled: institucionId !== '' && evaluacionId !== '' && entregaId !== '',
  });
}
