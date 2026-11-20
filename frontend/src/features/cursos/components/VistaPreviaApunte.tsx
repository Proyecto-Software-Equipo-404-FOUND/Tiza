import { ApiError } from '@/shared/api/cliente';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Modal } from '@/shared/components/Modal';
import { Spinner } from '@/shared/components/Spinner';
import { textoDisponible, type Material } from '../apuntes';
import { useTextoMaterial } from '../hooks/useTextoMaterial';
import { mensajeTextoApunte } from '../mensajesApuntes';

type Props = {
  /** Se conserva al cerrar para que el contenido no cambie durante la animación de salida */
  material: Material | null;
  abierto: boolean;
  onCerrar: () => void;
};

export const MENSAJE_SIN_TEXTO =
  'Este PDF no tiene texto seleccionable (puede ser un escaneo). La IA no va a poder usarlo.';

export function VistaPreviaApunte({ material, abierto, onCerrar }: Props) {
  const { data, isPending, isError, error, refetch } = useTextoMaterial(
    material?.cursoId ?? '',
    material?.id ?? '',
    abierto && material !== null,
  );

  // Con 403 y 404 repetir el pedido no sirve; con el 409 sí (el apunte puede terminar de procesarse)
  const sinReintento = error instanceof ApiError && (error.estado === 403 || error.estado === 404);

  return (
    <Modal
      abierto={abierto}
      titulo={`Texto de «${material?.titulo ?? ''}»`}
      subtitulo={data ? `${data.cantidadPaginas} páginas` : undefined}
      ancho='mediano'
      onCerrar={onCerrar}
    >
      <div className='flex flex-col gap-4'>
        {isPending && abierto && <Spinner texto='Cargando el texto…' />}

        {isError && (
          <BannerError mensaje={mensajeTextoApunte(error)} onReintentar={sinReintento ? undefined : () => refetch()} />
        )}

        {data && !textoDisponible(data) && <p className='text-tiza-bosque/80'>{MENSAJE_SIN_TEXTO}</p>}

        {data && textoDisponible(data) && (
          <div
            tabIndex={0}
            role='region'
            aria-label='Texto extraído del apunte'
            className='max-h-[50vh] overflow-y-auto rounded-xl border border-tiza-borde bg-white p-3'
          >
            <pre className='whitespace-pre-wrap break-words font-sans text-sm text-tiza-bosque'>{data.texto}</pre>
          </div>
        )}

        <Boton variante='secundario' onClick={onCerrar} className='self-end'>
          Cerrar
        </Boton>
      </div>
    </Modal>
  );
}