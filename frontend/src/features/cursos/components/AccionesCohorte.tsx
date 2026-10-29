import { AccionesFila } from '@/shared/components/AccionesFila';
import { useAviso } from '@/shared/components/Aviso';
import { mensajeDeError } from '@/shared/api/errores';
import type { Cohorte } from '@/shared/api/tipos';
import { useEliminarCohorte } from '../hooks/useEliminarCohorte';

type Props = {
  cursoId: string;
  cohorte: Cohorte;
  onEditar: (cohorte: Cohorte) => void;
  onEliminada: (id: string) => void;
};

export function AccionesCohorte({ cursoId, cohorte, onEditar, onEliminada }: Props) {
  const eliminar = useEliminarCohorte(cursoId);
  const { mostrar } = useAviso();

  return (
    <AccionesFila
      nombre={cohorte.nombre}
      eliminando={eliminar.isPending}
      onEditar={() => onEditar(cohorte)}
      onEliminar={() =>
        eliminar.mutate(cohorte.id, {
          onSuccess: () => {
            mostrar('exito', 'Cohorte eliminada.');
            onEliminada(cohorte.id);
          },
          onError: (error) => mostrar('error', mensajeDeError(error)),
        })
      }
    />
  );
}
