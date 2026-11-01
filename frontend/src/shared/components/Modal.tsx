import { useEffect, useId, useRef, useState } from 'react';
import type { KeyboardEvent, ReactNode } from 'react';
import { createPortal } from 'react-dom';

type Props = {
  abierto: boolean;
  titulo: string;
  subtitulo?: string;
  /** Texto o contenido que describe la ventana (lo lee el lector de pantalla) */
  descripcion?: ReactNode;
  children: ReactNode;
  /** Se llama al pedir cerrar (Escape o clic afuera) */
  onCerrar: () => void;
  /** Si es false, ni Escape ni el clic afuera cierran (por ejemplo, mientras se guarda) */
  cerrable?: boolean;
  /** Si es false, el clic afuera no cierra (útil en formularios, para no perder lo escrito) */
  cerrarAlClicAfuera?: boolean;
  /** Ancho máximo de la ventana */
  ancho?: 'chico' | 'mediano';
};

// Lo que dura la animación de entrada y de salida (milisegundos)
export const DURACION_MODAL = 200;

const FOCUSABLES = 'input:not(:disabled), select:not(:disabled), textarea:not(:disabled), button:not(:disabled)';

// Ventana emergente. Aparece y desaparece con un fundido suave
// (se desactiva si la persona pidió «reducir movimiento» en su sistema).
export function Modal({
  abierto,
  titulo,
  subtitulo,
  descripcion,
  children,
  onCerrar,
  cerrable = true,
  cerrarAlClicAfuera = true,
  ancho = 'chico',
}: Props) {
  // «montado»: sigue en el DOM durante la animación de salida. «visible»: ya tiene la clase final de la animación.
  const [montado, setMontado] = useState(abierto);
  const [visible, setVisible] = useState(false);
  const idTitulo = useId();
  const idDescripcion = useId();
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

  // Al abrir el foco pasa al primer campo o botón de la ventana
  useEffect(() => {
    if (abierto) panel.current?.querySelector<HTMLElement>(FOCUSABLES)?.focus();
  }, [abierto]);

  // Mientras está abierta no se mueve la página de atrás
  useEffect(() => {
    if (!montado) return;
    const antes = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => {
      document.body.style.overflow = antes;
    };
  }, [montado]);

  // Si está abierta se dibuja en este mismo render; «montado» solo la mantiene durante la salida
  if (!abierto && !montado) return null;

  const cerrar = () => {
    if (cerrable) onCerrar();
  };

  const alTeclear = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Escape') {
      e.stopPropagation();
      cerrar();
      return;
    }
    if (e.key !== 'Tab') return;
    // El foco se queda dentro de la ventana
    const campos = Array.from(panel.current?.querySelectorAll<HTMLElement>(FOCUSABLES) ?? []);
    if (campos.length === 0) return;
    const primero = campos[0];
    const ultimo = campos[campos.length - 1];
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
        if (cerrarAlClicAfuera && e.target === e.currentTarget) cerrar();
      }}
      onKeyDown={alTeclear}
    >
      <div
        ref={panel}
        role='dialog'
        aria-modal='true'
        aria-labelledby={idTitulo}
        aria-describedby={descripcion ? idDescripcion : undefined}
        className={`max-h-[90vh] w-full overflow-y-auto rounded-2xl bg-white p-6 shadow-tarjeta transition duration-200 motion-reduce:transition-none ${
          ancho === 'mediano' ? 'max-w-lg' : 'max-w-md'
        } ${mostrar ? 'translate-y-0 scale-100 opacity-100' : 'translate-y-2 scale-95 opacity-0'}`}
      >
        <h2 id={idTitulo} className='font-heading text-xl font-extrabold text-tiza-bosque'>
          {titulo}
        </h2>
        {subtitulo && <p className='mt-1 text-sm text-tiza-bosque/70'>{subtitulo}</p>}
        {descripcion && (
          <div id={idDescripcion} className='mt-2 text-tiza-bosque/80'>
            {descripcion}
          </div>
        )}
        <div className='mt-5'>{children}</div>
      </div>
    </div>,
    document.body,
  );
}
