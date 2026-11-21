import { useQuery } from '@tanstack/react-query';
import { obtenerTextoMaterial } from '../api/materiales';

// El texto se pide solo cuando se abre la vista previa. Sin reintentos automáticos:
// 403, 404 y 409 no se arreglan repitiendo el pedido (el 409 se reintenta a mano).
export function useTextoMaterial(cursoId: string, materialId: string, habilitado: boolean) {
  return useQuery({
    queryKey: ['texto-material', cursoId, materialId],
    queryFn: () => obtenerTextoMaterial(cursoId, materialId),
    enabled: habilitado && cursoId !== '' && materialId !== '',
    retry: false,
  });
}
