import { Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Boton } from './Boton';

type Props = {
  /** Nombre del elemento: se usa en los textos accesibles de los botones */
  nombre: string;
  onEditar: () => void;
  onEliminar: () => void;
  eliminando?: boolean;
};

const estiloChico = '!px-3 !py-1.5 text-sm whitespace-nowrap';

// Editar y eliminar para una fila. Eliminar pide una confirmación en la misma fila.
export function AccionesFila({ nombre, onEditar, onEliminar, eliminando = false }: Props) {
  const [confirmando, setConfirmando] = useState(false);

  if (confirmando) {
    return (
      <span role='group' aria-label={`Confirmar la eliminación de ${nombre}`} className='flex flex-wrap items-center gap-2'>
        <span className='text-sm font-semibold'>¿Eliminar?</span>
        <Boton
          variante='peligro'
          className={estiloChico}
          cargando={eliminando}
          onClick={() => {
            setConfirmando(false);
            onEliminar();
          }}
        >
          Sí, eliminar
        </Boton>
        <Boton variante='secundario' className={estiloChico} onClick={() => setConfirmando(false)}>
          Cancelar
        </Boton>
      </span>
    );
  }

  return (
    <span className='flex flex-nowrap items-center gap-2'>
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
