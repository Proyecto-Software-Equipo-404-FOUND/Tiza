import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { listarMisExamenes } from '../api/misExamenes';

// El id del alumno forma parte de la clave: los datos de una persona no se reutilizan para otra
export function useMisExamenes() {
  const { usuario } = useSesion();
  const alumnoId = usuario?.id ?? '';
  return useQuery({
    queryKey: ['mis-examenes', alumnoId],
    queryFn: listarMisExamenes,
    enabled: alumnoId !== '',
  });
}
