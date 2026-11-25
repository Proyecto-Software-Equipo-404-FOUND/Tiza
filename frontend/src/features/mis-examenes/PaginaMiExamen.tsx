import { CheckCircle2, ChevronLeft, XCircle } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { formatearFechaHora, formatearNota } from '@/features/revision-notas/formato';
import { useMiExamen } from './hooks/useMiExamen';
import { textoNotaAlumno } from './notaSobreDiez';
import type { MiRespuesta } from './tipos';

function Puntaje({ pregunta }: { pregunta: MiRespuesta }) {
  const texto = formatearNota(pregunta.puntajeObtenido, pregunta.puntajeMaximo);
  return (
    <span className='shrink-0 rounded-full bg-tiza-menta px-3 py-1 text-sm font-bold text-tiza-bosque'>
      {texto ?? `– / ${pregunta.puntajeMaximo}`}
    </span>
  );
}

function Pregunta({ pregunta, numero }: { pregunta: MiRespuesta; numero: number }) {
  const esMultiple = pregunta.tipo === 'OpcionMultiple';
  return (
    <li className='flex flex-col gap-3 py-5 first:pt-0 last:pb-0'>
      <div className='flex items-start justify-between gap-4'>
        <p className='font-semibold'>
          {numero}. {pregunta.enunciado}
        </p>
        <Puntaje pregunta={pregunta} />
      </div>

      {esMultiple ? (
        <div className='flex flex-col gap-1 text-sm'>
          {pregunta.opcionElegidaTexto ? (
            <p className='flex items-center gap-2'>
              {pregunta.esCorrecta ? (
                <CheckCircle2 className='h-5 w-5 shrink-0 text-tiza-pasto' aria-hidden='true' />
              ) : (
                <XCircle className='h-5 w-5 shrink-0 text-tiza-coral' aria-hidden='true' />
              )}
              <span>
                Tu respuesta: <strong>{pregunta.opcionElegidaTexto}</strong>{' '}
                <span className='sr-only'>{pregunta.esCorrecta ? '(correcta)' : '(incorrecta)'}</span>
              </span>
            </p>
          ) : (
            <p className='text-tiza-bosque/70'>No respondiste esta pregunta.</p>
          )}
          {!pregunta.esCorrecta && pregunta.opcionCorrectaTexto && (
            <p className='pl-7'>
              Respuesta correcta: <strong>{pregunta.opcionCorrectaTexto}</strong>
            </p>
          )}
        </div>
      ) : (
        <div className='flex flex-col gap-2 text-sm'>
          {pregunta.respuesta ? (
            <p className='whitespace-pre-wrap rounded-xl bg-tiza-fondo p-3'>{pregunta.respuesta}</p>
          ) : (
            <p className='text-tiza-bosque/70'>No respondiste esta pregunta.</p>
          )}
          {pregunta.justificacion && (
            <p>
              <span className='font-semibold'>Corrección: </span>
              {pregunta.justificacion}
            </p>
          )}
        </div>
      )}
    </li>
  );
}

export function PaginaMiExamen() {
  const { entregaId } = useParams();
  const { data: examen, isPending, isError, error, refetch } = useMiExamen(entregaId);
  const sinAprobar = error instanceof ApiError && error.estado === 409;
  const nota = examen ? textoNotaAlumno(examen.notaFinal, examen.puntajeMaximo) : null;

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <Link to='/mis-examenes' className='flex w-fit items-center gap-1 text-sm font-semibold text-tiza-bosque/80 hover:underline'>
        <ChevronLeft className='h-4 w-4' aria-hidden='true' />
        Mis exámenes
      </Link>

      {isPending && <Spinner texto='Cargando tu examen…' />}
      {isError &&
        (sinAprobar ? (
          <BannerError mensaje='Tu docente todavía no terminó de corregir este examen.' />
        ) : (
          <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />
        ))}

      {examen && (
        <>
          <div className='flex flex-col gap-1'>
            <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>{examen.titulo}</h1>
            <p className='text-sm text-tiza-bosque/70'>Entregaste el {formatearFechaHora(examen.fechaEntregaUtc)}</p>
            {nota && (
              <p className='mt-2 text-lg'>
                Nota: <strong>{nota.principal}</strong>
                {nota.detalle && <span className='ml-2 text-sm text-tiza-bosque/70'>({nota.detalle})</span>}
              </p>
            )}
          </div>
          <Tarjeta titulo='Tus respuestas' subtitulo='Puntaje de cada pregunta y qué estuvo bien o mal.'>
            <ol className='flex flex-col divide-y divide-tiza-bosque/10'>
              {examen.preguntas.map((pregunta, indice) => (
                <Pregunta key={pregunta.preguntaId} pregunta={pregunta} numero={indice + 1} />
              ))}
            </ol>
          </Tarjeta>
        </>
      )}
    </main>
  );
}
