import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarCurso } from '../api/cursos';

export function useActualizarCurso(id: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (datos: { nombre: string; area: string }) => actualizarCurso(id, datos),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['cursos'] }),
  });
}
