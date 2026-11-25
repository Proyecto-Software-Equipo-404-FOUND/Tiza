import { Link } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { formatearFechaHora } from '@/features/revision-notas/formato';
import { useMisExamenes } from './hooks/useMisExamenes';
import { textoNotaAlumno } from './notaSobreDiez';
import type { MiExamen } from './tipos';

// El alumno ve la nota solo cuando el docente la aprobó: antes de eso, nada de sugerencias de la IA
function Resultado({ examen }: { examen: MiExamen }) {
  if (examen.estado === 'Aprobada') {
    const nota = textoNotaAlumno(examen.notaFinal, examen.puntajeMaximo);
    return (
      <span className='flex flex-wrap items-center gap-2'>
        <span className='inline-block rounded-full bg-tiza-menta px-3 py-1 text-xs font-bold text-tiza-pasto'>
          Corregida
        </span>
        {nota && <span className='font-bold'>{nota.principal}</span>}
        {nota?.detalle && <span className='text-xs text-tiza-bosque/70'>({nota.detalle})</span>}
        {/* S3-EV-05: con la nota aprobada, el alumno puede ver qué respondió bien y mal */}
        <Link
          to={`/mis-examenes/${examen.entregaId}`}
          className='text-sm font-bold text-tiza-bosque underline'
          aria-label={`Ver la corrección de ${examen.titulo}`}
        >
          Ver corrección
        </Link>
      </span>
    );
  }
  return (
    <span className='inline-block rounded-full bg-tiza-lima/40 px-3 py-1 text-xs font-bold text-tiza-bosque'>
      Pendiente de corrección
    </span>
  );
}

export function PaginaMisExamenes() {
  const { data: examenes, isPending, isError, error, refetch } = useMisExamenes();

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Mis exámenes</h1>

      {isPending && <Spinner texto='Cargando tus exámenes…' />}
      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {examenes && (
        <Tarjeta
          titulo='Exámenes que rendiste'
          subtitulo='La nota aparece cuando tu docente la aprueba.'
        >
          {examenes.length === 0 ? (
            <p className='text-tiza-bosque/70'>Todavía no rendiste ningún examen.</p>
          ) : (
            // Lista en vez de tabla: en el celular el resultado queda debajo del título y se ve sin deslizar
            <ul className='flex flex-col divide-y divide-tiza-bosque/10'>
              {examenes.map((examen) => (
                <li
                  key={examen.entregaId}
                  className='flex flex-col gap-2 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between sm:gap-6'
                >
                  <div className='min-w-0'>
                    <p className='font-semibold'>{examen.titulo}</p>
                    <p className='text-sm text-tiza-bosque/70'>
                      Entregaste el {formatearFechaHora(examen.fechaEntregaUtc)}
                    </p>
                  </div>
                  <Resultado examen={examen} />
                </li>
              ))}
            </ul>
          )}
        </Tarjeta>
      )}
    </main>
  );
}
