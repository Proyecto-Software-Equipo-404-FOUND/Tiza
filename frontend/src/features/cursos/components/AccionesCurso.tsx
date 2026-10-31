import { AccionesFila } from '@/shared/components/AccionesFila';
import { useAviso } from '@/shared/components/Aviso';
import { mensajeDeError } from '@/shared/api/errores';
import type { Curso } from '@/shared/api/tipos';
import { useEliminarCurso } from '../hooks/useEliminarCurso';

type Props = { curso: Curso; onEditar: (curso: Curso) => void; onEliminado: (id: string) => void };

export function AccionesCurso({ curso, onEditar, onEliminado }: Props) {
  const eliminar = useEliminarCurso();
  const { mostrar } = useAviso();

  return (
    <AccionesFila
      nombre={curso.nombre}
      eliminando={eliminar.isPending}
      onEditar={() => onEditar(curso)}
      onEliminar={() =>
        eliminar.mutate(curso.id, {
          onSuccess: () => {
            mostrar('exito', 'Curso eliminado.');
            onEliminado(curso.id);
          },
          onError: (error) => mostrar('error', mensajeDeError(error)),
        })
      }
    />
  );
}
