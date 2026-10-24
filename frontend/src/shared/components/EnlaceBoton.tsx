import type { ReactNode } from 'react';
import { Link, type LinkProps } from 'react-router-dom';

type Props = LinkProps & { icono?: ReactNode };

// Enlace con aspecto de botón chico, para las acciones de cada fila de una tabla
export function EnlaceBoton({ icono, children, className = '', ...resto }: Props) {
  return (
    <Link
      {...resto}
      className={`inline-flex items-center gap-2 rounded-xl border border-tiza-borde bg-white px-3 py-1.5 text-sm font-bold text-tiza-bosque transition hover:bg-tiza-menta ${className}`}
    >
      {icono}
      {children}
    </Link>
  );
}
