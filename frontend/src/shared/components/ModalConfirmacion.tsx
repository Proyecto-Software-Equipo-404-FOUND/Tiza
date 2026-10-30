import { useEffect, useId, useRef, useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
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
  /** Mientras es true, el botón de confirmar muestra «Cargando…» y no se puede cerrar */
  cargando?: boolean;
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
  cargando = false,
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
      anterior.current = document.activeElement as HTMLElement | null;
    } else {
      anterior.current?.focus();
      anterior.current = null;
    }
  }, [abierto]);

  useEffect(() => {
    if (abierto) panel.current?.querySelector('button')?.focus();
  }, [abierto]);

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

  const cerrar = () => {
    if (!cargando) onCancelar();
  };

  const alTeclear = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Escape') {
      e.stopPropagation();
      cerrar();
      return;
    }
    if (e.key !== 'Tab') return;
    // El foco se queda dentro del cartel
    const botones = Array.from(panel.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)') ?? []);
    if (botones.length === 0) return;
    const primero = botones[0];
    const ultimo = botones[botones.length - 1];
    if (e.shiftKey && document.activeElement === primero) {
      e.preventDefault();
      ultimo.focus();
    } else if (!e.shiftKey && document.activeElement === ultimo) {
      e.preventDefault();
      primero.focus();
    }
  };

  const mostrar = visible && abierto;

  return createPortal(
    <div
      className={`fixed inset-0 z-40 flex items-center justify-center bg-tiza-pizarra/50 p-4 transition-opacity duration-200 motion-reduce:transition-none ${
        mostrar ? 'opacity-100' : 'opacity-0'
      }`}
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) cerrar();
      }}
      onKeyDown={alTeclear}
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
          <Boton type='button' variante='secundario' onClick={cerrar} disabled={cargando}>
            {textoCancelar}
          </Boton>
          <Boton type='button' variante={variante} cargando={cargando} onClick={onConfirmar}>
            {textoConfirmar}
          </Boton>
        </div>
      </div>
    </div>,
    document.body,
  );
}
