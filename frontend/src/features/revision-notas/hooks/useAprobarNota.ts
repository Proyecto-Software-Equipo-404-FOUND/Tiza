import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { aprobarNota, editarNotaRespuesta } from '../api/notaFinal';
import type { CambioNota } from '../notaFinal';

/**
 * Aprobar la nota son dos pasos: primero se guarda cada puntaje que el docente cambió (un pedido por pregunta)
 * y recién después se aprueba. Si falla un guardado no se aprueba; lo ya guardado queda y se puede reintentar.
 */
export function useAprobarNota(evaluacionId: string, entregaId: string) {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: async (cambios: CambioNota[]) => {
      for (const cambio of cambios) {
        await editarNotaRespuesta(evaluacionId, entregaId, cambio.preguntaId, institucionId, {
          puntaje: cambio.puntaje,
          ...(cambio.comentario !== undefined ? { comentario: cambio.comentario } : {}),
        });
      }
      return aprobarNota(evaluacionId, entregaId, institucionId);
    },
    // También si falló: pudo quedar algo guardado y la pantalla tiene que mostrarlo
    onSettled: () => cliente.invalidateQueries({ queryKey: ['entregas', institucionId, evaluacionId] }),
  });
}
