import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
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
      </span>
    );
  }
  return (
    <span className='inline-block rounded-full bg-tiza-lima/40 px-3 py-1 text-xs font-bold text-tiza-bosque'>
      Pendiente de corrección
    </span>
  );
}

const columnas: Columna<MiExamen>[] = [
  { titulo: 'Examen', celda: (examen) => <span className='font-semibold'>{examen.titulo}</span> },
  { titulo: 'Entregaste', celda: (examen) => formatearFechaHora(examen.fechaEntregaUtc) },
  { titulo: 'Resultado', celda: (examen) => <Resultado examen={examen} /> },
];

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
          <Tabla
            columnas={columnas}
            filas={examenes}
            clave={(examen) => examen.entregaId}
            textoVacio='Todavía no rendiste ningún examen.'
          />
        </Tarjeta>
      )}
    </main>
  );
}
