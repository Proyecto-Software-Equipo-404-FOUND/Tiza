import { useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { useAviso } from '@/shared/components/Aviso';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { borradorDesde } from '../borrador';
import { FormularioEvaluacion } from '../components/FormularioEvaluacion';
import { useActualizarEvaluacion } from '../hooks/useActualizarEvaluacion';
import { useEvaluacion } from '../hooks/useEvaluacion';

export function PaginaEditarEvaluacion() {
  const { evaluacionId = '' } = useParams<{ evaluacionId: string }>();
  const { data: evaluacion, isPending, isError, error, refetch } = useEvaluacion(evaluacionId);
  const actualizar = useActualizarEvaluacion(evaluacionId);
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const { mostrar } = useAviso();

  const irAlDetalle = () => navegar(`/evaluaciones/${evaluacionId}`);

  // Ante un 409 (por ejemplo, alguien la publicó mientras editabas) se vuelve a pedir y se sale de la edición
  const recargar = () => {
    actualizar.reset();
    cliente.invalidateQueries({ queryKey: ['evaluaciones'] });
    irAlDetalle();
  };

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <Link to={`/evaluaciones/${evaluacionId}`} className='text-sm font-semibold text-tiza-pasto hover:underline'>
        ← Volver a la evaluación
      </Link>

      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Editar evaluación</h1>
        {evaluacion && (
          <p className='mt-1 text-tiza-bosque/70'>Estás editando «{evaluacion.titulo}».</p>
        )}
      </header>

      {isPending && <Spinner texto='Cargando la evaluación…' />}

      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {evaluacion && evaluacion.estado !== 'Borrador' && (
        <Tarjeta titulo='Esta evaluación ya no se puede editar'>
          <p className='text-tiza-bosque/80'>Solo se pueden editar las evaluaciones en borrador.</p>
        </Tarjeta>
      )}

      {evaluacion && evaluacion.estado === 'Borrador' && (
        <FormularioEvaluacion
          key={evaluacion.id}
          inicial={borradorDesde(evaluacion)}
          textoEnviar='Guardar cambios'
          enviando={actualizar.isPending}
          error={actualizar.error}
          onEnviar={(datos) =>
            actualizar.mutate(datos, {
              onSuccess: () => {
                mostrar('exito', 'Cambios guardados.');
                irAlDetalle();
              },
            })
          }
          onCancelar={irAlDetalle}
          onRecargar={recargar}
        />
      )}
    </main>
  );
}
