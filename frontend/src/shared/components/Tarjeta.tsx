import type { ReactNode } from 'react';

type Props = {
  titulo: string;
  subtitulo?: string;
  /** Ícono o logo a la izquierda del título */
  icono?: ReactNode;
  children?: ReactNode;
  className?: string;
};

export function Tarjeta({ titulo, subtitulo, icono, children, className = '' }: Props) {
  return (
    <section className={`rounded-2xl border border-tiza-borde bg-white p-6 shadow-tarjeta ${className}`}>
      <div className='mb-4 flex items-start gap-3'>
        {icono}
        <div className='min-w-0'>
          <h2 className='font-heading text-xl font-extrabold text-tiza-bosque'>{titulo}</h2>
          {subtitulo && <p className='text-sm text-tiza-bosque/70'>{subtitulo}</p>}
        </div>
      </div>
      {children}
    </section>
  );
}
