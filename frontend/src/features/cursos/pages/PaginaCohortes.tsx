import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { Modal } from '@/shared/components/Modal';
import { EnlaceBoton } from '@/shared/components/EnlaceBoton';
import { GraduationCap } from 'lucide-react';
import { mensajeDeError } from '@/shared/api/errores';
import type { Cohorte } from '@/shared/api/tipos';
import { useCurso } from '../hooks/useCurso';
import { useCohortes } from '../hooks/useCohortes';
import { AccionesCohorte } from '../components/AccionesCohorte';
import { FormularioCohorte } from '../components/FormularioCohorte';
import { FormularioEditarCohorte } from '../components/FormularioEditarCohorte';

const crearColumnas = (
  cursoId: string,
  onEditar: (cohorte: Cohorte) => void,
  onEliminada: (id: string) => void,
): Columna<Cohorte>[] => [
  { titulo: 'Nombre', celda: (cohorte) => cohorte.nombre },
  { titulo: 'Período', celda: (cohorte) => cohorte.periodo },
  {
    titulo: 'Matrícula',
    celda: (cohorte) => (
      <EnlaceBoton
        to={`/cohortes/${cohorte.id}/matriculas`}
        icono={<GraduationCap className='h-4 w-4' aria-hidden='true' />}
        className='whitespace-nowrap'
      >
        Ver matrícula
      </EnlaceBoton>
    ),
  },
  {
    titulo: 'Acciones',
    celda: (cohorte) => (
      <AccionesCohorte cursoId={cursoId} cohorte={cohorte} onEditar={onEditar} onEliminada={onEliminada} />
    ),
  },
];

export function PaginaCohortes() {
  const { cursoId = '' } = useParams();
  const { data: curso } = useCurso(cursoId);
  const { data: cohortes, isPending, isError, error, refetch } = useCohortes(cursoId);
  // La cohorte se conserva al cerrar la ventana para que el formulario no desaparezca durante la animación de salida
  const [enEdicion, setEnEdicion] = useState<Cohorte | null>(null);
  const [editando, setEditando] = useState(false);

  const editar = (cohorte: Cohorte) => {
    setEnEdicion(cohorte);
    setEditando(true);
  };
  // Si se elimina la cohorte que se está editando, se cierra la ventana
  const alEliminar = (id: string) => {
    if (enEdicion?.id === id) setEditando(false);
  };
  const columnas = crearColumnas(cursoId, editar, alEliminar);

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

      <Modal
        abierto={editando}
        titulo='Editar cohorte'
        subtitulo={enEdicion ? `Estás editando «${enEdicion.nombre}».` : undefined}
        onCerrar={() => setEditando(false)}
        cerrarAlClicAfuera={false}
        ancho='mediano'
      >
        {enEdicion && (
          <FormularioEditarCohorte
            key={enEdicion.id}
            cursoId={cursoId}
            cohorte={enEdicion}
            onTerminar={() => setEditando(false)}
          />
        )}
      </Modal>
    </main>
  );
}