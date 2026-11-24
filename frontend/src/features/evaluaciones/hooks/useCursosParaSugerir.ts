import { useQuery } from '@tanstack/react-query';
import { listarCursosParaSugerir } from '../api/sugerencias';

// `habilitado` evita pedir los cursos hasta que el docente abre el panel
export function useCursosParaSugerir(habilitado: boolean) {
  return useQuery({
    queryKey: ['sugerencias', 'cursos'],
    queryFn: listarCursosParaSugerir,
    enabled: habilitado,
  });
}
