import { useCursos } from '../hooks/useCursos';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { mensajeDeError } from '@/shared/api/errores';
import type { Curso } from '@/shared/api/tipos';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { FormularioCurso } from '../components/FormularioCurso';

const columnas: Columna<Curso>[] = [{ titulo: 'Nombre', celda: (curso) => curso.nombre }];

export function PaginaCursos() {
  const { data: cursos, isPending, isError, error, refetch } = useCursos();

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-4 p-6'>
      <h1 className='text-2xl font-bold text-tiza-pizarra'>Cursos</h1>
        <Tarjeta titulo='Nuevo curso'>
            <FormularioCurso />
        </Tarjeta>

      {isPending && <Spinner texto='Cargando cursos…' />}

      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {cursos && (
        <Tabla
          columnas={columnas}
          filas={cursos}
          clave={(curso) => curso.id}
          textoVacio='Todavía no hay cursos.'
        />
      )}
    </main>
  );
}