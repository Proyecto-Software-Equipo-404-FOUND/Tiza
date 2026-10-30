import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Boton } from './Boton';
import { ModalConfirmacion } from './ModalConfirmacion';

type Props = {
  /** Nombre del elemento: se usa en los textos accesibles de los botones */
  nombre: string;
  onEditar: () => void;
  onEliminar: () => void;
  eliminando?: boolean;
};

const estiloChico = '!px-3 !py-1.5 text-sm whitespace-nowrap';

// Editar y eliminar para una fila. Eliminar pide confirmación en un cartel emergente.
export function AccionesFila({ nombre, onEditar, onEliminar, eliminando = false }: Props) {
  const [confirmando, setConfirmando] = useState(false);

  return (
    <span className='flex flex-nowrap items-center gap-2'>
      <ModalConfirmacion
        abierto={confirmando}
        titulo={`¿Eliminar «${nombre}»?`}
        mensaje='Esta acción no se puede deshacer.'
        onCancelar={() => setConfirmando(false)}
        onConfirmar={() => {
          setConfirmando(false);
          onEliminar();
        }}
      />
      <Boton variante='secundario' className={estiloChico} onClick={onEditar} aria-label={`Editar ${nombre}`}>
        <Pencil className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
        Editar
      </Boton>
      <Boton
        variante='secundario'
        className={`${estiloChico} !text-tiza-coral`}
        onClick={() => setConfirmando(true)}
        disabled={eliminando}
        aria-label={`Eliminar ${nombre}`}
      >
        <Trash2 className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
        Eliminar
      </Boton>
    </span>
  );
}
