import { Link, useParams } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { PestanasEvaluacion } from '@/shared/components/PestanasEvaluacion';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { useEvaluacion } from '@/features/evaluaciones/hooks/useEvaluacion';
import { formatearNota, formatearNumero } from '@/shared/lib/numeros';
import { useNombresAlumnos } from '@/features/revision-notas/hooks/useNombresAlumnos';
import { describirDiferencia, porcentajeParaBarra, preguntaMasDificil } from './calculos';
import { useMetricas } from './hooks/useMetricas';
import type { MetricaAlumno, MetricasEvaluacion } from './tipos';

const estiloDiferencia = {
  arriba: 'text-tiza-pasto',
  abajo: 'text-tiza-coral',
  igual: 'text-tiza-bosque/70',
} as const;

type PropsDato = { etiqueta: string; valor: string; detalle?: string; acortar?: boolean };

// `acortar`: un texto largo (el enunciado de una pregunta) se corta en 2 líneas y se ve completo al apoyar el mouse
// y en la lista «Por pregunta», así la tarjeta no se agranda
function Dato({ etiqueta, valor, detalle, acortar = false }: PropsDato) {
  return (
    <div className='flex flex-col gap-1 rounded-2xl border border-tiza-borde bg-white p-5 shadow-tarjeta'>
      <p className='text-sm font-semibold text-tiza-bosque/70'>{etiqueta}</p>
      <p
        title={acortar ? valor : undefined}
        className={`font-heading font-extrabold text-tiza-bosque ${acortar ? 'line-clamp-2 break-words text-lg' : 'text-2xl'}`}
      >
        {valor}
      </p>
      {detalle && <p className='text-sm text-tiza-bosque/70'>{detalle}</p>}
    </div>
  );
}

function Contenido({
  metricas,
  nombreDe,
}: {
  metricas: MetricasEvaluacion;
  nombreDe: (alumnoId: string, nombreInvitado?: string | null) => string;
}) {
  if (metricas.cantidadEntregas === 0) {
    return (
      <Tarjeta titulo='Todavía no hay métricas'>
        <p className='text-tiza-bosque/80'>Todavía nadie entregó este examen.</p>
      </Tarjeta>
    );
  }

  // Las métricas solo cuentan las notas ya corregidas (aprobadas por el docente)
  if (metricas.cantidadAprobadas === 0) {
    return (
      <Tarjeta titulo='Todavía no hay notas corregidas'>
        <p className='text-tiza-bosque/80'>
          Las métricas se calculan con las notas que ya aprobaste. Hay {metricas.cantidadEntregas}{' '}
          {metricas.cantidadEntregas === 1 ? 'entrega' : 'entregas'} para corregir en la pestaña Resultados.
        </p>
      </Tarjeta>
    );
  }

  const masDificil = preguntaMasDificil(metricas);
  const alumnos = [...metricas.alumnos].sort((a, b) => b.notaFinal - a.notaFinal);

  const columnas: Columna<MetricaAlumno>[] = [
    { titulo: 'Alumno', celda: (alumno) => <span className='font-semibold'>{nombreDe(alumno.alumnoId, alumno.nombreInvitado)}</span> },
    { titulo: 'Nota final', celda: (alumno) => formatearNota(alumno.notaFinal, metricas.puntajeMaximo) },
    {
      titulo: 'Contra el promedio',
      celda: (alumno) => {
        const { texto, tono } = describirDiferencia(alumno.diferenciaConPromedio);
        return <span className={`font-semibold ${estiloDiferencia[tono]}`}>{texto}</span>;
      },
    },
  ];

  return (
    <>
      <div className='grid gap-4 md:grid-cols-3'>
        <Dato
          etiqueta='Promedio del examen'
          valor={metricas.promedio == null ? '—' : (formatearNota(metricas.promedio, metricas.puntajeMaximo) ?? '—')}
          detalle='Solo cuentan las notas corregidas'
        />
        <Dato
          etiqueta='Pregunta más difícil'
          valor={masDificil ? masDificil.enunciado : '—'}
          acortar
          detalle={masDificil ? `${formatearNumero(masDificil.porcentaje)} % de acierto promedio` : undefined}
        />
        <Dato
          etiqueta='Notas corregidas'
          valor={`${metricas.cantidadAprobadas} de ${metricas.cantidadEntregas}`}
          detalle='Entregas con la nota corregida'
        />
      </div>

      <Tarjeta titulo='Por pregunta' subtitulo='Qué porcentaje del puntaje consiguió, en promedio, cada pregunta.'>
        <ul className='flex flex-col gap-5'>
          {metricas.preguntas.map((pregunta, indice) => {
            const ancho = porcentajeParaBarra(pregunta.porcentaje);
            return (
              <li key={pregunta.preguntaId} className='flex flex-col gap-2'>
                <div className='flex flex-wrap items-start justify-between gap-2'>
                  <p className='min-w-0 font-semibold'>
                    {indice + 1}. {pregunta.enunciado}
                  </p>
                  {pregunta.preguntaId === metricas.preguntaMasDificilId && (
                    <span className='rounded-full bg-tiza-lima/40 px-3 py-1 text-xs font-bold text-tiza-bosque'>
                      Más difícil
                    </span>
                  )}
                </div>
                <div
                  role='progressbar'
                  aria-label={`Acierto promedio de la pregunta ${indice + 1}`}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-valuenow={ancho}
                  className='h-2.5 w-full overflow-hidden rounded-full bg-tiza-menta'
                >
                  <div className='h-full rounded-full bg-tiza-pasto' style={{ width: `${ancho}%` }} />
                </div>
                <p className='text-sm text-tiza-bosque/70'>
                  {formatearNota(pregunta.promedio, pregunta.puntajeMaximo)} · {formatearNumero(pregunta.porcentaje)} %
                </p>
              </li>
            );
          })}
        </ul>
      </Tarjeta>

      <Tarjeta titulo='Alumnos contra el promedio' subtitulo='De la nota más alta a la más baja.'>
        <Tabla
          columnas={columnas}
          filas={alumnos}
          clave={(alumno) => alumno.alumnoId}
          textoVacio='Todavía no hay alumnos con la nota corregida.'
        />
      </Tarjeta>
    </>
  );
}

export function PaginaMetricas() {
  const { evaluacionId = '' } = useParams<{ evaluacionId: string }>();
  const { data: evaluacion } = useEvaluacion(evaluacionId);
  const { data: metricas, isPending, isError, error, refetch } = useMetricas(evaluacionId);
  const { nombreDe, fallo: fallaronLosNombres } = useNombresAlumnos();

  return (
    <main className='mx-auto flex max-w-5xl flex-col gap-6 p-6'>
      <Link to='/evaluaciones' className='text-sm font-semibold text-tiza-pasto hover:underline'>
        ← Volver a evaluaciones
      </Link>

      <header className='flex flex-col gap-4'>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>
          {evaluacion ? evaluacion.titulo : 'Métricas'}
        </h1>
        <PestanasEvaluacion evaluacionId={evaluacionId} actual='metricas' />
      </header>

      {isPending && <Spinner texto='Cargando las métricas…' />}
      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}
      {fallaronLosNombres && (
        <BannerError mensaje='No pudimos cargar los nombres de los alumnos. Mostramos un identificador corto en su lugar.' />
      )}

      {metricas && <Contenido metricas={metricas} nombreDe={nombreDe} />}
    </main>
  );
}
