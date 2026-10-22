import { Link, useParams } from 'react-router-dom';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { mensajeDeError } from '@/shared/api/errores';
import type { Cohorte } from '@/shared/api/tipos';
import { useCursos } from '../hooks/useCursos';
import { useCohortes } from '../hooks/useCohortes';
import { FormularioCohorte } from '../components/FormularioCohorte';

const columnas: Columna<Cohorte>[] = [
  { titulo: 'Nombre', celda: (cohorte) => cohorte.nombre },
  {
    titulo: 'Matrícula',
    celda: (cohorte) => (
      <Link to={`/cohortes/${cohorte.id}/matriculas`} className='text-tiza-violeta underline'>
        Ver matrícula
      </Link>
    ),
  },
];

export function PaginaCohortes() {
  const { cursoId = '' } = useParams();
  // No hay endpoint para un solo curso: se busca en la lista, que ya está en caché
  const { data: cursos } = useCursos();
  const { data: cohortes, isPending, isError, error, refetch } = useCohortes(cursoId);
  const curso = cursos?.find((c) => c.id === cursoId);

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-4 p-6'>
      <Link to='/cursos' className='text-sm text-tiza-violeta underline'>
        ← Volver a cursos
      </Link>
      <h1 className='text-2xl font-bold text-tiza-pizarra'>
        Cohortes{curso ? ` de ${curso.nombre}` : ''}
      </h1>

      <Tarjeta titulo='Nueva cohorte'>
        <FormularioCohorte cursoId={cursoId} />
      </Tarjeta>

      {isPending && <Spinner texto='Cargando cohortes…' />}

      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {cohortes && (
        <Tabla
          columnas={columnas}
          filas={cohortes}
          clave={(cohorte) => cohorte.id}
          textoVacio='Todavía no hay cohortes en este curso.'
        />
      )}
    </main>
  );
}