import { useMutation, useQueryClient } from '@tanstack/react-query';
import { crearCohorte } from '../api/cohortes';

export function useCrearCohorte(cursoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (datos: { nombre: string }) => crearCohorte(cursoId, datos),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['cohortes', cursoId] }),
  });
}