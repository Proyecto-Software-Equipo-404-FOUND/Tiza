import type { ReactNode } from 'react';
import { Boton } from './Boton';
import { Modal } from './Modal';

export { DURACION_MODAL } from './Modal';

type Props = {
  abierto: boolean;
  titulo: string;
  /** Texto o contenido que explica qué va a pasar */
  mensaje: ReactNode;
  textoConfirmar?: string;
  textoCancelar?: string;
  variante?: 'principal' | 'peligro';
  onConfirmar: () => void;
  onCancelar: () => void;
};

// Cartel emergente de confirmación, armado sobre la ventana base `Modal`
export function ModalConfirmacion({
  abierto,
  titulo,
  mensaje,
  textoConfirmar = 'Sí, eliminar',
  textoCancelar = 'Cancelar',
  variante = 'peligro',
  onConfirmar,
  onCancelar,
}: Props) {
  return (
    <Modal abierto={abierto} titulo={titulo} descripcion={mensaje} onCerrar={onCancelar}>
      <div className='flex flex-col-reverse gap-3 sm:flex-row sm:justify-end'>
        <Boton type='button' variante='secundario' onClick={onCancelar}>
          {textoCancelar}
        </Boton>
        <Boton type='button' variante={variante} onClick={onConfirmar}>
          {textoConfirmar}
        </Boton>
      </div>
    </Modal>
  );
}
