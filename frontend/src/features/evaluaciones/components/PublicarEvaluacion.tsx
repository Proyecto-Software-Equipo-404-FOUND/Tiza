import { useQueryClient } from '@tanstack/react-query';
import { Send } from 'lucide-react';
import { useState } from 'react';
import { useCohortes } from '@/features/cursos/hooks/useCohortes';
import { useCursos } from '@/features/cursos/hooks/useCursos';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { useAviso } from '@/shared/components/Aviso';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { CampoSelect } from '@/shared/components/CampoSelect';
import { Spinner } from '@/shared/components/Spinner';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { usePublicarEvaluacion } from '../hooks/usePublicarEvaluacion';
import type { Evaluacion } from '../tipos';

type Props = { evaluacion: Evaluacion };

export function PublicarEvaluacion({ evaluacion }: Props) {
  const [cursoId, setCursoId] = useState('');
  const [cohorteId, setCohorteId] = useState('');
  const cursos = useCursos();
  const cohortes = useCohortes(cursoId);
  const publicar = usePublicarEvaluacion(evaluacion.id);
  const cliente = useQueryClient();
  const { mostrar } = useAviso();

  const sinPreguntas = evaluacion.preguntasJson.preguntas.length === 0;
  const hayConflicto = publicar.error instanceof ApiError && publicar.error.estado === 409;

  // Ante un 409 la evaluación cambió (otra edición o ya publicada): se vuelve a pedir y se muestra lo actual
  const recargar = () => {
    publicar.reset();
    cliente.invalidateQueries({ queryKey: ['evaluaciones'] });
  };

  const enviar = () =>
    publicar.mutate(
      // La versión es la que se está viendo: si cambió en el servidor, responde 409
      { versionEsperada: evaluacion.version, cohorteId },
      { onSuccess: () => mostrar('exito', 'Evaluación publicada.') },
    );

  return (
    <Tarjeta
      titulo='Publicar evaluación'
      subtitulo='Una vez publicada ya no se puede editar ni borrar.'
      icono={
        <span className='flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-tiza-pasto'>
          <Send className='h-6 w-6' aria-hidden='true' />
        </span>
      }
    >
      <div className='flex flex-col gap-5'>
        {cursos.isPending && <Spinner texto='Cargando cursos…' />}
        {cursos.isError && <BannerError mensaje={mensajeDeError(cursos.error)} onReintentar={() => cursos.refetch()} />}

        {cursos.data && (
          <CampoSelect
            etiqueta='Curso'
            value={cursoId}
            onChange={(evento) => {
              setCursoId(evento.target.value);
              setCohorteId('');
            }}
            disabled={publicar.isPending}
          >
            <option value=''>Elegí un curso</option>
            {cursos.data.items.map((curso) => (
              <option key={curso.id} value={curso.id}>
                {curso.nombre}
              </option>
            ))}
          </CampoSelect>
        )}

        {cursoId !== '' && cohortes.isPending && <Spinner texto='Cargando cohortes…' />}
        {cursoId !== '' && cohortes.isError && (
          <BannerError mensaje={mensajeDeError(cohortes.error)} onReintentar={() => cohortes.refetch()} />
        )}

        {cursoId !== '' && cohortes.data && (
          <CampoSelect
            etiqueta='Cohorte'
            value={cohorteId}
            onChange={(evento) => setCohorteId(evento.target.value)}
            disabled={publicar.isPending}
          >
            <option value=''>{cohortes.data.length === 0 ? 'Este curso no tiene cohortes' : 'Elegí una cohorte'}</option>
            {cohortes.data.map((cohorte) => (
              <option key={cohorte.id} value={cohorte.id}>
                {cohorte.nombre} · {cohorte.periodo}
              </option>
            ))}
          </CampoSelect>
        )}

        {sinPreguntas && <p className='text-sm text-tiza-bosque/70'>Agregá al menos una pregunta para poder publicar.</p>}

        {Boolean(publicar.error) && (
          <BannerError
            mensaje={mensajeDeError(publicar.error)}
            onReintentar={hayConflicto ? recargar : undefined}
            textoBoton='Recargar'
          />
        )}

        <Boton
          type='button'
          className='self-start'
          cargando={publicar.isPending}
          disabled={cohorteId === '' || sinPreguntas}
          onClick={enviar}
        >
          Publicar
        </Boton>
      </div>
    </Tarjeta>
  );
}
