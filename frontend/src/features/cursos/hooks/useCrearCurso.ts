import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crearCurso } from '../api/cursos';

export function useCrearCurso() {
  const cliente = useQueryClient();

  return useMutation({
    mutationFn: crearCurso,
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['cursos'] }),
  });
}