import type { ButtonHTMLAttributes } from 'react';

type Variante = 'principal' | 'secundario' | 'peligro';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variante?: Variante;
  cargando?: boolean;
};

const estilos: Record<Variante, string> = {
  principal: 'bg-tiza-lima text-tiza-bosque hover:brightness-95',
  secundario: 'border border-tiza-borde bg-white text-tiza-bosque hover:bg-tiza-menta',
  peligro: 'bg-tiza-coral text-white hover:brightness-95',
};

export function Boton({
  variante = 'principal',
  cargando = false,
  disabled,
  children,
  className = '',
  ...resto
}: Props) {
  return (
    <button
      {...resto}
      disabled={disabled || cargando}
      aria-busy={cargando}
      className={`rounded-xl px-5 py-3 font-bold transition disabled:cursor-not-allowed disabled:opacity-50 ${estilos[variante]} ${className}`}
    >
      {cargando ? 'Cargando…' : children}
    </button>
  );
}
