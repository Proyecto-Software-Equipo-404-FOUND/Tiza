import { useMutation, useQueryClient } from '@tanstack/react-query';
import { eliminarCurso } from '../api/cursos';

export function useEliminarCurso() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: eliminarCurso,
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['cursos'] }),
  });
}
