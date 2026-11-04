import { useEffect, useId, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Boton } from './Boton';

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

// Lo que dura la animación de entrada y de salida (milisegundos)
export const DURACION_MODAL = 200;

// Cartel emergente de confirmación. Aparece y desaparece con un fundido suave
// (se desactiva si la persona pidió «reducir movimiento» en su sistema).
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
  // «montado»: está en el DOM. «visible»: ya tiene la clase final de la animación.
  const [montado, setMontado] = useState(abierto);
  const [visible, setVisible] = useState(false);
  const idTitulo = useId();
  const idMensaje = useId();
  const panel = useRef<HTMLDivElement>(null);
  const anterior = useRef<HTMLElement | null>(null);
  const seAbrio = useRef(false);

  useEffect(() => {
    if (abierto) {
      setMontado(true);
      const t = setTimeout(() => setVisible(true), 20);
      return () => clearTimeout(t);
    }
    setVisible(false);
    const t = setTimeout(() => setMontado(false), DURACION_MODAL);
    return () => clearTimeout(t);
  }, [abierto]);

  // Al abrir se recuerda qué botón estaba enfocado y al cerrar se le devuelve el foco
  useEffect(() => {
    if (abierto) {
      seAbrio.current = true;
      anterior.current = document.activeElement as HTMLElement | null;
    } else {
      anterior.current?.focus();
      anterior.current = null;
    }
  }, [abierto]);

  useEffect(() => {
    if (abierto) panel.current?.querySelector('button')?.focus();
  }, [abierto]);

  // Si al borrar desaparece el botón de origen (por ejemplo, la fila), el foco no se pierde en el body:
  // pasa al título de la página
  useEffect(
    () => () => {
      if (!seAbrio.current) return;
      const activo = document.activeElement;
      if (activo && activo !== document.body) return;
      const destino = document.querySelector<HTMLElement>('main h1') ?? document.querySelector<HTMLElement>('main');
      if (!destino) return;
      destino.tabIndex = -1;
      destino.focus({ preventScroll: true });
    },
    [],
  );

  // Escape y Tab se escuchan en todo el documento: si se hizo clic en el texto del cartel el foco
  // queda en el body y, de otro modo, el teclado no llegaría hasta acá
  useEffect(() => {
    if (!abierto) return;
    const alTeclear = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onCancelar();
        return;
      }
      if (e.key !== 'Tab') return;
      const botones = Array.from(panel.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)') ?? []);
      if (botones.length === 0) return;
      const primero = botones[0];
      const ultimo = botones[botones.length - 1];
      const dentro = panel.current?.contains(document.activeElement) ?? false;
      if (!dentro) {
        e.preventDefault();
        (e.shiftKey ? ultimo : primero).focus();
      } else if (e.shiftKey && document.activeElement === primero) {
        e.preventDefault();
        ultimo.focus();
      } else if (!e.shiftKey && document.activeElement === ultimo) {
        e.preventDefault();
        primero.focus();
      }
    };
    document.addEventListener('keydown', alTeclear);
    return () => document.removeEventListener('keydown', alTeclear);
  }, [abierto, onCancelar]);

  // Mientras está abierto no se mueve la página de atrás
  useEffect(() => {
    if (!montado) return;
    const antes = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = antes;
    };
  }, [montado]);

  // Si está abierto se dibuja en este mismo render; «montado» solo mantiene el cartel durante la salida
  if (!abierto && !montado) return null;

  const mostrar = visible && abierto;

  return createPortal(
    <div
      className={`fixed inset-0 z-40 flex items-center justify-center bg-tiza-pizarra/50 p-4 transition-opacity duration-200 motion-reduce:transition-none ${
        mostrar ? 'opacity-100' : 'opacity-0'
      }`}
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onCancelar();
      }}
    >
      <div
        ref={panel}
        role='dialog'
        aria-modal='true'
        aria-labelledby={idTitulo}
        aria-describedby={idMensaje}
        className={`w-full max-w-md rounded-2xl bg-white p-6 shadow-tarjeta transition duration-200 motion-reduce:transition-none ${
          mostrar ? 'translate-y-0 scale-100 opacity-100' : 'translate-y-2 scale-95 opacity-0'
        }`}
      >
        <h2 id={idTitulo} className='font-heading text-xl font-extrabold text-tiza-bosque'>
          {titulo}
        </h2>
        <div id={idMensaje} className='mt-2 text-tiza-bosque/80'>
          {mensaje}
        </div>
        <div className='mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end'>
          <Boton type='button' variante='secundario' onClick={onCancelar}>
            {textoCancelar}
          </Boton>
          <Boton type='button' variante={variante} onClick={onConfirmar}>
            {textoConfirmar}
          </Boton>
        </div>
      </div>
    </div>,
    document.body,
  );
}
