import { useState, type ReactNode } from 'react';
import { ChevronRight, GraduationCap, Sparkles, Users } from 'lucide-react';
import { useCursos } from '../hooks/useCursos';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { mensajeDeError } from '@/shared/api/errores';
import type { Curso } from '@/shared/api/tipos';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { Modal } from '@/shared/components/Modal';
import { EnlaceBoton } from '@/shared/components/EnlaceBoton';
import { FormularioCurso } from '../components/FormularioCurso';
import { AccionesCurso } from '../components/AccionesCurso';
import { FormularioEditarCurso } from '../components/FormularioEditarCurso';
import { IconoArea } from '../components/IconoArea';

const crearColumnas = (
  onEditar: (curso: Curso) => void,
  onEliminado: (id: string) => void,
): Columna<Curso>[] => [
  {
    titulo: 'Nombre',
    celda: (curso) => (
      <span className='flex items-center gap-3 font-semibold'>
        <IconoArea area={curso.area} />
        {curso.nombre}
      </span>
    ),
  },
  { titulo: 'Área', celda: (curso) => curso.area },
  {
    titulo: 'Cohortes',
    celda: (curso) => (
      <EnlaceBoton
        to={`/cursos/${curso.id}/cohortes`}
        icono={<Users className='h-4 w-4' aria-hidden='true' />}
        className='whitespace-nowrap bg-tiza-menta/60'
      >
        Ver cohortes
        <ChevronRight className='h-4 w-4' aria-hidden='true' />
      </EnlaceBoton>
    ),
  },
  {
    titulo: 'Acciones',
    celda: (curso) => <AccionesCurso curso={curso} onEditar={onEditar} onEliminado={onEliminado} />,
  },
];

const circulo = (icono: ReactNode) => (
  <span className='flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-tiza-pasto'>
    {icono}
  </span>
);

export function PaginaCursos() {
  const { data: resultado, isPending, isError, error, refetch } = useCursos();
  // El curso se conserva al cerrar la ventana para que el formulario no desaparezca durante la animación de salida
  const [enEdicion, setEnEdicion] = useState<Curso | null>(null);
  const [editando, setEditando] = useState(false);

  const editar = (curso: Curso) => {
    setEnEdicion(curso);
    setEditando(true);
  };
  // Si se elimina el curso que se está editando, se cierra la ventana
  const alEliminar = (id: string) => {
    if (enEdicion?.id === id) setEditando(false);
  };
  const columnas = crearColumnas(editar, alEliminar);

  return (
    <main className='mx-auto flex max-w-6xl flex-col gap-6 p-6'>
      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Cursos</h1>
        <p className='mt-1 text-tiza-bosque/70'>
          Organizá tus cursos y accedé a sus cohortes y matrículas.
        </p>
      </header>

      <div className='grid grid-cols-1 items-start gap-6 lg:grid-cols-[2fr_3fr]'>
        <Tarjeta
          titulo='Crear nuevo curso'
          subtitulo='Completá la información para crear un nuevo curso.'
          icono={circulo(<GraduationCap className='h-6 w-6' aria-hidden='true' />)}
        >
          <FormularioCurso />
        </Tarjeta>

        <Tarjeta
          titulo='Tus cursos activos'
          subtitulo='Los cursos que ya creaste y sus cohortes.'
          icono={circulo(<Users className='h-6 w-6' aria-hidden='true' />)}
        >
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
                <p className='mt-3 text-sm text-tiza-bosque'>
                  Mostrando {resultado.items.length} de {resultado.totalItems} cursos.
                </p>
              )}
            </>
          )}

          <p className='mt-4 flex items-center gap-2 rounded-xl bg-tiza-menta/60 px-4 py-3 text-sm text-tiza-bosque/80'>
            <Sparkles className='h-4 w-4 shrink-0 text-tiza-pasto' aria-hidden='true' />
            La IA te ayuda a corregir y a devolver feedback en minutos.
          </p>
        </Tarjeta>
      </div>

      <Modal
        abierto={editando}
        titulo='Editar curso'
        subtitulo={enEdicion ? `Estás editando «${enEdicion.nombre}».` : undefined}
        onCerrar={() => setEditando(false)}
        cerrarAlClicAfuera={false}
        ancho='mediano'
      >
        {enEdicion && (
          <FormularioEditarCurso key={enEdicion.id} curso={enEdicion} onTerminar={() => setEditando(false)} />
        )}
      </Modal>
    </main>
  );
}
