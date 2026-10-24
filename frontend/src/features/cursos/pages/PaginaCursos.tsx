import { Link } from 'react-router-dom';
import { useCursos } from '../hooks/useCursos';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { mensajeDeError } from '@/shared/api/errores';
import type { Curso } from '@/shared/api/tipos';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { FormularioCurso } from '../components/FormularioCurso';

const columnas: Columna<Curso>[] = [
  { titulo: 'Nombre', celda: (curso) => curso.nombre },
  { titulo: 'Área', celda: (curso) => curso.area },
  {
    titulo: 'Cohortes',
    celda: (curso) => (
      <Link to={`/cursos/${curso.id}/cohortes`} className='text-tiza-violeta underline'>
        Ver cohortes
      </Link>
    ),
  },
];

export function PaginaCursos() {
  const { data: resultado, isPending, isError, error, refetch } = useCursos();

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-4 p-6'>
      <h1 className='text-2xl font-bold text-tiza-pizarra'>Cursos</h1>

      <Tarjeta titulo='Nuevo curso'>
        <FormularioCurso />
      </Tarjeta>

      {isPending && <Spinner texto='Cargando cursos…' />}

      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {resultado && (
        <>
          <Tabla
            columnas={columnas}
            filas={resultado.items}
            clave={(curso) => curso.id}
            textoVacio='Todavía no hay cursos.'
          />
          {resultado.totalItems > resultado.items.length && (
            <p className='text-sm text-tiza-pizarra'>
              Mostrando {resultado.items.length} de {resultado.totalItems} cursos.
            </p>
          )}
        </>
      )}
    </main>
  );
}