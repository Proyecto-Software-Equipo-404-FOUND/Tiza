import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Trash2 } from 'lucide-react';
import { Spinner } from '@/shared/components/Spinner';
import { BannerError } from '@/shared/components/BannerError';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { Boton } from '@/shared/components/Boton';
import { ModalConfirmacion } from '@/shared/components/ModalConfirmacion';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { useCurso } from '../hooks/useCurso';
import { useMateriales } from '../hooks/useMateriales';
import { useEliminarMaterial } from '../hooks/useEliminarMaterial';
import { FormularioApunte } from '../components/FormularioApunte';
import { etiquetaEstado, formatearTamano, type Material } from '../apuntes';
import { mensajeApuntes } from '../mensajesApuntes';

const estiloChico = '!px-3 !py-1.5 text-sm whitespace-nowrap';

const crearColumnas = (
  onEliminar: (material: Material) => void,
  eliminando: boolean,
): Columna<Material>[] => [
  { titulo: 'Título', celda: (material) => material.titulo },
  { titulo: 'Páginas', celda: (material) => material.cantidadPaginas },
  { titulo: 'Tamaño', celda: (material) => formatearTamano(material.tamanoBytes) },
  {
    titulo: 'Fecha',
    celda: (material) => new Date(material.fechaSubidaUtc).toLocaleDateString('es-AR'),
  },
  { titulo: 'Estado', celda: (material) => etiquetaEstado(material.estado) },
  {
    titulo: 'Acciones',
    celda: (material) => (
      <Boton
        variante='secundario'
        className={`${estiloChico} !text-tiza-coral`}
        onClick={() => onEliminar(material)}
        disabled={eliminando}
        aria-label={`Eliminar ${material.titulo}`}
      >
        <Trash2 className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
        Eliminar
      </Boton>
    ),
  },
];

export function PaginaApuntes() {
  const { cursoId = '' } = useParams();
  const { data: curso } = useCurso(cursoId);
  const { data: materiales, isPending, isError, error, refetch } = useMateriales(cursoId);
  const eliminar = useEliminarMaterial(cursoId);
  const { mostrar } = useAviso();
  // El apunte se conserva al cerrar el cartel para que el texto no cambie durante la animación de salida
  const [aEliminar, setAEliminar] = useState<Material | null>(null);
  const [confirmando, setConfirmando] = useState(false);

  const pedirConfirmacion = (material: Material) => {
    setAEliminar(material);
    setConfirmando(true);
  };

  const confirmar = () => {
    setConfirmando(false);
    if (!aEliminar) return;
    eliminar.mutate(aEliminar.id, {
      onSuccess: () => mostrar('exito', 'Apunte eliminado.'),
      onError: (falla) => mostrar('error', mensajeApuntes(falla)),
    });
  };

  const sinPermiso = error instanceof ApiError && error.estado === 403;
  const columnas = crearColumnas(pedirConfirmacion, eliminar.isPending);

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-4 p-6'>
      <Link to='/cursos' className='text-sm text-tiza-pasto underline'>
        ← Volver a cursos
      </Link>
      <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>
        Apuntes{curso ? ` de ${curso.nombre}` : ''}
      </h1>

      {!sinPermiso && (
        <Tarjeta titulo='Subir apunte'>
          <FormularioApunte cursoId={cursoId} />
        </Tarjeta>
      )}

      {isPending && <Spinner texto='Cargando apuntes…' />}

      {isError && (
        <BannerError
          mensaje={mensajeApuntes(error)}
          onReintentar={sinPermiso ? undefined : () => refetch()}
        />
      )}

      {materiales && (
        <Tabla
          columnas={columnas}
          filas={materiales}
          clave={(material) => material.id}
          textoVacio='Todavía no hay apuntes en este curso.'
        />
      )}

      <ModalConfirmacion
        abierto={confirmando}
        titulo={`¿Eliminar «${aEliminar?.titulo ?? ''}»?`}
        mensaje='Esta acción no se puede deshacer.'
        onCancelar={() => setConfirmando(false)}
        onConfirmar={confirmar}
      />
    </main>
  );
}