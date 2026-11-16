import { useState, type ReactNode } from 'react';
import { Archive, FileText, GraduationCap, Sparkles, Users } from 'lucide-react';
import { useEsAdministrador } from '@/features/auth/useEsAdministrador';
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
  conAcciones: boolean,
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
      </EnlaceBoton>
    ),
  },
  {
    titulo: 'Apuntes',
    celda: (curso) => (
      <EnlaceBoton
        to={`/cursos/${curso.id}/apuntes`}
        icono={<FileText className='h-4 w-4' aria-hidden='true' />}
        className='whitespace-nowrap bg-tiza-menta/60'
      >
        Ver apuntes
      </EnlaceBoton>
    ),
  },
  // Editar y eliminar son solo del administrador: al docente no se le ofrece lo que el servidor rechaza
  ...(conAcciones
    ? [
        {
          titulo: 'Acciones',
          celda: (curso: Curso) => (
            <AccionesCurso curso={curso} onEditar={onEditar} onEliminado={onEliminado} />
          ),
        },
      ]
    : []),
];

// Cursos dados de baja: solo lectura (la baja es lógica, el servidor los sigue devolviendo con activo=false)
const columnasInactivos: Columna<Curso>[] = [
  {
    titulo: 'Nombre',
    celda: (curso) => (
      <span className='flex items-center gap-3 font-semibold text-tiza-bosque/70'>
        <IconoArea area={curso.area} />
        {curso.nombre}
      </span>
    ),
  },
  { titulo: 'Área', celda: (curso) => curso.area },
  { titulo: 'Estado', celda: () => 'Inactivo' },
];

const circulo = (icono: ReactNode) => (
  <span className='flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-tiza-pasto'>
    {icono}
  </span>
);

export function PaginaCursos() {
  const { data: resultado, isPending, isError, error, refetch } = useCursos();
  const esAdministrador = useEsAdministrador();
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
  const activos = resultado?.items.filter((curso) => curso.activo) ?? [];
  const inactivos = resultado?.items.filter((curso) => !curso.activo) ?? [];
  const columnas = crearColumnas(editar, alEliminar, esAdministrador);

  return (
    <main className='mx-auto flex max-w-7xl flex-col gap-6 p-6'>
      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Cursos</h1>
        <p className='mt-1 text-tiza-bosque/70'>
          {esAdministrador
            ? 'Organizá tus cursos y accedé a sus cohortes y matrículas.'
            : 'Accedé a los cursos, sus cohortes, matrículas y apuntes.'}
        </p>
      </header>

      <div
        className={`grid grid-cols-1 items-start gap-6 ${esAdministrador ? 'lg:grid-cols-[20rem_minmax(0,1fr)]' : ''}`}
      >
        {esAdministrador && (
          <Tarjeta
            titulo='Crear nuevo curso'
            subtitulo='Completá la información para crear un nuevo curso.'
            icono={circulo(<GraduationCap className='h-6 w-6' aria-hidden='true' />)}
          >
            <FormularioCurso />
          </Tarjeta>
        )}

        <Tarjeta
          titulo='Tus cursos activos'
          subtitulo={esAdministrador ? 'Los cursos que ya creaste y sus cohortes.' : 'Los cursos y sus cohortes.'}
          icono={circulo(<Users className='h-6 w-6' aria-hidden='true' />)}
        >
          {isPending && <Spinner texto='Cargando cursos…' />}

          {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

          {resultado && (
            <>
              <Tabla
                columnas={columnas}
                filas={activos}
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

      {resultado && (
        <Tarjeta
          titulo='Cursos inactivos'
          subtitulo='Los cursos eliminados. Ya no se les pueden asignar exámenes.'
          icono={circulo(<Archive className='h-6 w-6' aria-hidden='true' />)}
        >
          <Tabla
            columnas={columnasInactivos}
            filas={inactivos}
            clave={(curso) => curso.id}
            textoVacio='No hay cursos inactivos.'
          />
        </Tarjeta>
      )}

      {esAdministrador && (
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
      )}
    </main>
  );
}
