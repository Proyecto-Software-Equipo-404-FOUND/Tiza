import { Check } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { InsigniaEstado } from '../components/InsigniaEstado';
import { etiquetaTipoPregunta, formatearFecha, puntajeTotal } from '../formato';
import { useEvaluacion } from '../hooks/useEvaluacion';
import type { Criterio, Pregunta } from '../tipos';

const columnasCriterios: Columna<Criterio>[] = [
  { titulo: 'Criterio', celda: (criterio) => criterio.descripcion },
  { titulo: 'Puntaje', celda: (criterio) => criterio.puntaje },
];

function TarjetaPregunta({ pregunta, numero }: { pregunta: Pregunta; numero: number }) {
  const criterios = pregunta.rubrica?.criterios ?? [];
  return (
    <li className='rounded-2xl border border-tiza-borde bg-white p-5'>
      <div className='flex flex-wrap items-start justify-between gap-2'>
        <h3 className='font-heading text-lg font-extrabold text-tiza-bosque'>
          {numero}. {pregunta.enunciado}
        </h3>
        <span className='flex shrink-0 gap-2 text-xs font-bold'>
          <span className='rounded-full bg-tiza-menta px-3 py-1 text-tiza-pasto'>
            {etiquetaTipoPregunta(pregunta.tipo)}
          </span>
          <span className='rounded-full bg-tiza-fondo px-3 py-1 text-tiza-bosque'>
            {pregunta.puntajeMaximo} pts
          </span>
        </span>
      </div>

      {pregunta.opciones && pregunta.opciones.length > 0 && (
        <ul className='mt-3 flex flex-col gap-2'>
          {pregunta.opciones.map((opcion) => (
            <li
              key={opcion.id}
              className={`flex items-center gap-2 rounded-xl border px-3 py-2 ${
                opcion.esCorrecta ? 'border-tiza-pasto bg-tiza-menta/60 font-semibold' : 'border-tiza-borde'
              }`}
            >
              {opcion.esCorrecta && <Check className='h-4 w-4 text-tiza-pasto' aria-hidden='true' />}
              {opcion.texto}
              {opcion.esCorrecta && <span className='sr-only'>(respuesta correcta)</span>}
            </li>
          ))}
        </ul>
      )}

      {criterios.length > 0 && (
        <div className='mt-4'>
          <p className='mb-2 text-sm font-bold text-tiza-bosque'>Rúbrica</p>
          <Tabla columnas={columnasCriterios} filas={criterios} clave={(criterio) => criterio.descripcion} />
        </div>
      )}
    </li>
  );
}

export function PaginaEvaluacion() {
  const { evaluacionId = '' } = useParams<{ evaluacionId: string }>();
  const { data: evaluacion, isPending, isError, error, refetch } = useEvaluacion(evaluacionId);

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <Link to='/evaluaciones' className='text-sm font-semibold text-tiza-pasto hover:underline'>
        ← Volver a evaluaciones
      </Link>

      {isPending && <Spinner texto='Cargando la evaluación…' />}

      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {evaluacion && (
        <>
          <header>
            <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>{evaluacion.titulo}</h1>
            <p className='mt-3 flex flex-wrap items-center gap-3 text-sm text-tiza-bosque/80'>
              <InsigniaEstado estado={evaluacion.estado} />
              <span>{evaluacion.tipo}</span>
              <span>Versión {evaluacion.version}</span>
              <span>Actualizada el {formatearFecha(evaluacion.fechaActualizacion)}</span>
            </p>
          </header>

          <Tarjeta titulo='Resumen'>
            <p>
              {evaluacion.preguntasJson.preguntas.length}{' '}
              {evaluacion.preguntasJson.preguntas.length === 1 ? 'pregunta' : 'preguntas'} ·{' '}
              {puntajeTotal(evaluacion.preguntasJson.preguntas)} puntos en total
            </p>
          </Tarjeta>

          <section aria-labelledby='titulo-preguntas' className='flex flex-col gap-3'>
            <h2 id='titulo-preguntas' className='font-heading text-2xl font-extrabold text-tiza-bosque'>
              Preguntas
            </h2>
            {evaluacion.preguntasJson.preguntas.length === 0 ? (
              <p className='text-tiza-bosque/70'>Esta evaluación todavía no tiene preguntas.</p>
            ) : (
              <ol className='flex flex-col gap-4'>
                {evaluacion.preguntasJson.preguntas.map((pregunta, indice) => (
                  <TarjetaPregunta key={pregunta.id} pregunta={pregunta} numero={indice + 1} />
                ))}
              </ol>
            )}
          </section>
        </>
      )}
    </main>
  );
}
