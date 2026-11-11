import { useQuery } from '@tanstack/react-query';
import { listarMateriales } from '../api/materiales';

export function useMateriales(cursoId: string) {
  return useQuery({
    queryKey: ['materiales', cursoId],
    queryFn: () => listarMateriales(cursoId),
  });
}