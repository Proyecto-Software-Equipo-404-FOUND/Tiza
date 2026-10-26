import { Link, useParams } from 'react-router-dom';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { EnlaceBoton } from '@/shared/components/EnlaceBoton';
import { GraduationCap } from 'lucide-react';
import { mensajeDeError } from '@/shared/api/errores';
import type { Cohorte } from '@/shared/api/tipos';
import { useCurso } from '../hooks/useCurso';
import { useCohortes } from '../hooks/useCohortes';
import { FormularioCohorte } from '../components/FormularioCohorte';

const columnas: Columna<Cohorte>[] = [
  { titulo: 'Nombre', celda: (cohorte) => cohorte.nombre },
  { titulo: 'Período', celda: (cohorte) => cohorte.periodo },
  {
    titulo: 'Matrícula',
    celda: (cohorte) => (
      <EnlaceBoton
        to={`/cohortes/${cohorte.id}/matriculas`}
        icono={<GraduationCap className='h-4 w-4' aria-hidden='true' />}
      >
        Ver matrícula
      </EnlaceBoton>
    ),
  },
];

export function PaginaCohortes() {
  const { cursoId = '' } = useParams();
  const { data: curso } = useCurso(cursoId);
  const { data: cohortes, isPending, isError, error, refetch } = useCohortes(cursoId);

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-4 p-6'>
      <Link to='/cursos' className='text-sm text-tiza-pasto underline'>
        ← Volver a cursos
      </Link>
      <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>
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