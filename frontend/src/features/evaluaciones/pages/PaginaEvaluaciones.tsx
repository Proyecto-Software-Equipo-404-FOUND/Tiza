import { ChevronRight, ClipboardList, FileText } from 'lucide-react';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { EnlaceBoton } from '@/shared/components/EnlaceBoton';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { InsigniaEstado } from '../components/InsigniaEstado';
import { formatearFecha } from '../formato';
import { useEvaluaciones } from '../hooks/useEvaluaciones';
import type { Evaluacion } from '../tipos';

const columnas: Columna<Evaluacion>[] = [
  {
    titulo: 'Título',
    celda: (evaluacion) => (
      <span className='flex items-center gap-3 font-semibold'>
        <span className='flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-tiza-menta text-tiza-pasto'>
          <FileText className='h-5 w-5' aria-hidden='true' />
        </span>
        {evaluacion.titulo}
      </span>
    ),
  },
  { titulo: 'Tipo', celda: (evaluacion) => evaluacion.tipo },
  { titulo: 'Estado', celda: (evaluacion) => <InsigniaEstado estado={evaluacion.estado} /> },
  { titulo: 'Preguntas', celda: (evaluacion) => evaluacion.preguntasJson.preguntas.length },
  { titulo: 'Actualizada', celda: (evaluacion) => formatearFecha(evaluacion.fechaActualizacion) },
  {
    titulo: 'Detalle',
    celda: (evaluacion) => (
      <EnlaceBoton to={`/evaluaciones/${evaluacion.id}`} className='whitespace-nowrap bg-tiza-menta/60'>
        Ver detalle
        <ChevronRight className='h-4 w-4' aria-hidden='true' />
      </EnlaceBoton>
    ),
  },
];

export function PaginaEvaluaciones() {
  const { data: evaluaciones, isPending, isError, error, refetch } = useEvaluaciones();

  return (
    <main className='mx-auto flex max-w-6xl flex-col gap-6 p-6'>
      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Evaluaciones</h1>
        <p className='mt-1 text-tiza-bosque/70'>
          Las evaluaciones de tu institución, en borrador o ya publicadas.
        </p>
      </header>

      <Tarjeta
        titulo='Tus evaluaciones'
        subtitulo='Entrá a una para ver sus preguntas y rúbricas.'
        icono={
          <span className='flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-tiza-pasto'>
            <ClipboardList className='h-6 w-6' aria-hidden='true' />
          </span>
        }
      >
        {isPending && <Spinner texto='Cargando evaluaciones…' />}

        {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

        {evaluaciones && (
          <Tabla
            columnas={columnas}
            filas={[...evaluaciones].sort((a, b) => b.fechaActualizacion.localeCompare(a.fechaActualizacion))}
            clave={(evaluacion) => evaluacion.id}
            textoVacio='Todavía no hay evaluaciones.'
          />
        )}
      </Tarjeta>
    </main>
  );
}
