import { useQuery } from '@tanstack/react-query';
import { listarApuntesParaSugerir } from '../api/sugerencias';

export function useApuntesParaSugerir(cursoId: string) {
  return useQuery({
    queryKey: ['sugerencias', 'apuntes', cursoId],
    queryFn: () => listarApuntesParaSugerir(cursoId),
    enabled: cursoId !== '',
  });
}
