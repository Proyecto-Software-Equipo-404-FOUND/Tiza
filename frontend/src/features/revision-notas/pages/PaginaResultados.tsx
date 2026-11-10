import { Link, useParams } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { useEvaluacion } from '@/features/evaluaciones/hooks/useEvaluacion';
import { AlertasFoco, EtiquetaSugerida, InsigniaEstadoEntrega } from '../components/InsigniasResultados';
import { PestanasEvaluacion } from '../components/PestanasEvaluacion';
import { formatearFechaHora, formatearNota } from '../formato';
import { useEntregas } from '../hooks/useEntregas';
import { useNombresAlumnos } from '../hooks/useNombresAlumnos';
import type { EntregaResumen } from '../tipos';

export function PaginaResultados() {
  const { evaluacionId = '' } = useParams<{ evaluacionId: string }>();
  const { data: evaluacion } = useEvaluacion(evaluacionId);
  const { data: entregas, isPending, isError, error, refetch } = useEntregas(evaluacionId);
  const { nombreDe, fallo: fallaronLosNombres } = useNombresAlumnos();

  const columnas: Columna<EntregaResumen>[] = [
    {
      titulo: 'Alumno',
      celda: (entrega) => (
        <Link
          to={`/evaluaciones/${evaluacionId}/resultados/${entrega.entregaId}`}
          className='font-semibold text-tiza-pasto hover:underline'
        >
          {nombreDe(entrega.alumnoId)}
        </Link>
      ),
    },
    {
      titulo: 'Estado',
      celda: (entrega) => (
        <span className='flex flex-col items-start gap-1'>
          <InsigniaEstadoEntrega estado={entrega.estado} />
          {entrega.respondioAlgo === false && <span className='text-xs text-tiza-bosque/70'>No respondió</span>}
        </span>
      ),
    },
    {
      titulo: 'Múltiple choice',
      celda: (entrega) =>
        formatearNota(entrega.notaMultipleChoice, entrega.puntajeMaximoMultipleChoice) ?? (
          <span className='text-tiza-bosque/70'>—</span>
        ),
    },
    {
      titulo: 'Nota de la IA',
      celda: (entrega) => {
        // Sin preguntas abiertas no hay nada que sugerir; con abiertas y sin nota, la IA todavía no terminó
        if (entrega.puntajeMaximoAbiertas == null) return <span className='text-tiza-bosque/70'>—</span>;
        const nota = formatearNota(entrega.notaSugeridaAbiertas, entrega.puntajeMaximoAbiertas);
        return nota ? (
          <span className='flex flex-wrap items-center gap-2'>
            {nota}
            <EtiquetaSugerida />
          </span>
        ) : (
          <span className='text-tiza-bosque/70'>Pendiente</span>
        );
      },
    },
    { titulo: 'Alertas de foco', celda: (entrega) => <AlertasFoco alertas={entrega.alertasFoco} /> },
    { titulo: 'Entregó', celda: (entrega) => formatearFechaHora(entrega.fechaEntregaUtc) },
  ];

  return (
    <main className='mx-auto flex max-w-5xl flex-col gap-6 p-6'>
      <Link to='/evaluaciones' className='text-sm font-semibold text-tiza-pasto hover:underline'>
        ← Volver a evaluaciones
      </Link>

      <header className='flex flex-col gap-4'>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>
          {evaluacion ? evaluacion.titulo : 'Resultados'}
        </h1>
        <PestanasEvaluacion evaluacionId={evaluacionId} actual='resultados' />
      </header>

      {isPending && <Spinner texto='Cargando los resultados…' />}
      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}
      {fallaronLosNombres && (
        <BannerError mensaje='No pudimos cargar los nombres de los alumnos. Mostramos un identificador corto en su lugar.' />
      )}

      {entregas && (
        <Tarjeta
          titulo='Entregas'
          subtitulo='La nota de la IA es solo una sugerencia: no es la nota final. Las alertas de foco son un aviso y no modifican la nota.'
        >
          <Tabla
            columnas={columnas}
            filas={entregas}
            clave={(entrega) => entrega.entregaId}
            textoVacio='Todavía nadie entregó este examen.'
          />
        </Tarjeta>
      )}
    </main>
  );
}
