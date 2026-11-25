import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { obtenerMiExamen } from '../api/misExamenes';

// S3-EV-05: el id del alumno forma parte de la clave, igual que en useMisExamenes
export function useMiExamen(entregaId: string | undefined) {
  const { usuario } = useSesion();
  const alumnoId = usuario?.id ?? '';
  return useQuery({
    queryKey: ['mis-examenes', alumnoId, entregaId],
    queryFn: () => obtenerMiExamen(entregaId as string),
    enabled: alumnoId !== '' && !!entregaId,
  });
}
