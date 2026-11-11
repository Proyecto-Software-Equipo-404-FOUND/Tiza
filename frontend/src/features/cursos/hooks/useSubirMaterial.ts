import { useMutation, useQueryClient } from '@tanstack/react-query';
import { subirMaterial } from '../api/materiales';

export function useSubirMaterial(cursoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ archivo, titulo }: { archivo: File; titulo?: string }) =>
      subirMaterial(cursoId, archivo, titulo),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['materiales', cursoId] }),
  });
}