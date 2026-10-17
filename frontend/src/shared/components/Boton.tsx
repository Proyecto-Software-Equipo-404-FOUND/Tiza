import type { ButtonHTMLAttributes } from 'react';

type Variante = 'principal' | 'secundario' | 'peligro';

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variante?: Variante;
  cargando?: boolean;
};

const estilos: Record<Variante, string> = {
  principal: 'bg-tiza-lima text-tiza-pizarra',
  secundario: 'border border-tiza-pizarra bg-transparent text-tiza-pizarra',
  peligro: 'bg-tiza-coral text-tiza-pizarra',
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
      className={`rounded-md px-4 py-2 font-bold disabled:cursor-not-allowed disabled:opacity-50 ${estilos[variante]} ${className}`}
    >
      {cargando ? 'Cargando…' : children}
    </button>
  );
}
