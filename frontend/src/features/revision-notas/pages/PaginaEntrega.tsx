import { Check, X } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { AlertasFoco, EtiquetaSugerida, InsigniaEstadoEntrega } from '../components/InsigniasResultados';
import { esRoja, formatearDuracion, formatearFechaHora, formatearNota } from '../formato';
import { useEntrega } from '../hooks/useEntregas';
import { useNombresAlumnos } from '../hooks/useNombresAlumnos';
import type { AlertaFocoDetalle, DesgloseCriterio, RespuestaEntrega } from '../tipos';

const columnasDesglose: Columna<DesgloseCriterio>[] = [
  { titulo: 'Criterio', celda: (fila) => fila.criterio ?? '—' },
  { titulo: 'Puntaje', celda: (fila) => (fila.puntaje == null ? '—' : fila.puntaje) },
  { titulo: 'Comentario', celda: (fila) => fila.comentario ?? '' },
];

function RespuestaMultipleChoice({ respuesta }: { respuesta: RespuestaEntrega }) {
  if (!respuesta.respuesta) return <p className='text-tiza-bosque/70'>No respondió.</p>;
  return (
    <p className='flex flex-wrap items-center gap-2'>
      {respuesta.esCorrecta ? (
        <Check className='h-4 w-4 text-tiza-pasto' aria-hidden='true' />
      ) : (
        <X className='h-4 w-4 text-tiza-coral' aria-hidden='true' />
      )}
      <span>
        Eligió: <strong>{respuesta.opcionElegidaTexto ?? respuesta.respuesta}</strong>
      </span>
      <span className='text-sm font-semibold text-tiza-bosque/80'>
        {respuesta.esCorrecta ? '(correcta)' : '(incorrecta)'}
        {respuesta.puntajeObtenido != null && ` · ${formatearNota(respuesta.puntajeObtenido, respuesta.puntajeMaximo)}`}
      </span>
    </p>
  );
}

function RespuestaAbierta({ respuesta }: { respuesta: RespuestaEntrega }) {
  const desglose = respuesta.desglosePorCriterio ?? [];
  return (
    <div className='flex flex-col gap-3'>
      {respuesta.respuesta ? (
        <p className='whitespace-pre-wrap rounded-xl bg-tiza-fondo p-3'>{respuesta.respuesta}</p>
      ) : (
        <p className='text-tiza-bosque/70'>No respondió.</p>
      )}

      {respuesta.estadoCorreccion === 'PendienteIA' && (
        <p className='text-sm text-amber-700'>La IA todavía no corrigió esta respuesta.</p>
      )}

      {respuesta.notaSugerida != null && (
        <div className='flex flex-col gap-2 rounded-xl border border-violet-200 bg-violet-50 p-3'>
          <p className='flex flex-wrap items-center gap-2 font-semibold text-tiza-bosque'>
            Nota de la IA: {formatearNota(respuesta.notaSugerida, respuesta.puntajeMaximo)}
            <EtiquetaSugerida />
          </p>
          {respuesta.justificacionIA && (
            <p className='text-sm text-tiza-bosque/80'>
              <span className='font-bold'>Justificación de la IA: </span>
              {respuesta.justificacionIA}
            </p>
          )}
          {desglose.length > 0 && (
            <Tabla columnas={columnasDesglose} filas={desglose} clave={(fila) => fila.criterio ?? String(fila.puntaje)} />
          )}
        </div>
      )}
    </div>
  );
}

function TarjetaRespuesta({ respuesta, numero }: { respuesta: RespuestaEntrega; numero: number }) {
  return (
    <li className='rounded-2xl border border-tiza-borde bg-white p-5'>
      <h3 className='mb-3 font-heading text-lg font-extrabold text-tiza-bosque'>
        {numero}. {respuesta.enunciado}
      </h3>
      {respuesta.tipo === 'OpcionMultiple' ? (
        <RespuestaMultipleChoice respuesta={respuesta} />
      ) : (
        <RespuestaAbierta respuesta={respuesta} />
      )}
    </li>
  );
}

const columnasAlertas: Columna<AlertaFocoDetalle>[] = [
  {
    titulo: 'Gravedad',
    celda: (alerta) =>
      esRoja(alerta) ? (
        <span className='rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-bold text-red-700'>Roja</span>
      ) : (
        <span className='rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-700'>Amarilla</span>
      ),
  },
  { titulo: 'Estuvo fuera', celda: (alerta) => formatearDuracion(alerta.duracionMs) },
  { titulo: 'Cuándo', celda: (alerta) => formatearFechaHora(alerta.registradoUtc) },
];

export function PaginaEntrega() {
  const { evaluacionId = '', entregaId = '' } = useParams<{ evaluacionId: string; entregaId: string }>();
  const { data: entrega, isPending, isError, error, refetch } = useEntrega(evaluacionId, entregaId);
  const { nombreDe } = useNombresAlumnos();

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <Link
        to={`/evaluaciones/${evaluacionId}/resultados`}
        className='text-sm font-semibold text-tiza-pasto hover:underline'
      >
        ← Volver a resultados
      </Link>

      {isPending && <Spinner texto='Cargando la entrega…' />}
      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {entrega && (
        <>
          <header>
            <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>{nombreDe(entrega.alumnoId)}</h1>
            <p className='mt-3 flex flex-wrap items-center gap-3 text-sm text-tiza-bosque/80'>
              <InsigniaEstadoEntrega estado={entrega.estado} />
              <span>Entregó el {formatearFechaHora(entrega.fechaEntregaUtc)}</span>
              {entrega.respondioAlgo === false && <span>No respondió nada</span>}
            </p>
          </header>

          <Tarjeta
            titulo='Notas'
            subtitulo='La nota de la IA es solo una sugerencia, todavía no es la nota final.'
          >
            <dl className='grid gap-4 sm:grid-cols-3'>
              <div>
                <dt className='text-sm font-bold text-tiza-bosque/70'>Múltiple choice</dt>
                <dd>{formatearNota(entrega.notaMultipleChoice, entrega.puntajeMaximoMultipleChoice) ?? '—'}</dd>
              </div>
              <div>
                <dt className='text-sm font-bold text-tiza-bosque/70'>Abiertas (IA)</dt>
                <dd className='flex flex-wrap items-center gap-2'>
                  {entrega.puntajeMaximoAbiertas == null
                    ? '—'
                    : (formatearNota(entrega.notaSugeridaAbiertas, entrega.puntajeMaximoAbiertas) ?? 'Pendiente')}
                  {entrega.notaSugeridaAbiertas != null && <EtiquetaSugerida />}
                </dd>
              </div>
              <div>
                <dt className='text-sm font-bold text-tiza-bosque/70'>Alertas de foco</dt>
                <dd>
                  <AlertasFoco alertas={entrega.alertasFoco} />
                </dd>
              </div>
            </dl>
          </Tarjeta>

          <section aria-labelledby='titulo-respuestas' className='flex flex-col gap-3'>
            <h2 id='titulo-respuestas' className='font-heading text-2xl font-extrabold text-tiza-bosque'>
              Respuestas
            </h2>
            {entrega.respuestas.length === 0 ? (
              <p className='text-tiza-bosque/70'>No hay respuestas para mostrar.</p>
            ) : (
              <ol className='flex flex-col gap-4'>
                {entrega.respuestas.map((respuesta, indice) => (
                  <TarjetaRespuesta key={respuesta.preguntaId} respuesta={respuesta} numero={indice + 1} />
                ))}
              </ol>
            )}
          </section>

          {entrega.alertas.length > 0 && (
            <Tarjeta
              titulo='Salidas de la pantalla'
              subtitulo='Es un aviso: no modifica la nota. Amarilla: menos de 3 s. Roja: 3 s o más.'
            >
              <Tabla columnas={columnasAlertas} filas={entrega.alertas} clave={(alerta) => `${alerta.registradoUtc}-${alerta.tipoEvento}-${alerta.duracionMs}`} />
            </Tarjeta>
          )}
        </>
      )}
    </main>
  );
}
