import { useMutation, useQueryClient } from '@tanstack/react-query';
import { eliminarCohorte } from '../api/cohortes';

export function useEliminarCohorte(cursoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => eliminarCohorte(cursoId, id),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['cohortes', cursoId] }),
  });
}
