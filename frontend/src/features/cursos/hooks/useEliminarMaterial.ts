import { useMutation, useQueryClient } from '@tanstack/react-query';
import { eliminarMaterial } from '../api/materiales';

export function useEliminarMaterial(cursoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => eliminarMaterial(cursoId, id),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['materiales', cursoId] }),
  });
}