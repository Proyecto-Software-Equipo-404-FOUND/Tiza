import { useMutation, useQueryClient } from '@tanstack/react-query';
import { actualizarCohorte } from '../api/cohortes';

export function useActualizarCohorte(cursoId: string, id: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (datos: { nombre: string; periodo: string; docenteId: string }) =>
      actualizarCohorte(cursoId, id, datos),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['cohortes', cursoId] }),
  });
}
