import type { ReactNode } from 'react';

type Props = {
  titulo: string;
  children?: ReactNode;
  className?: string;
};

export function Tarjeta({ titulo, children, className = '' }: Props) {
  return (
    <section className={`rounded-lg border border-tiza-pizarra/20 bg-white p-4 shadow-sm ${className}`}>
      <h2 className='mb-2 font-heading text-lg font-bold text-tiza-pizarra'>{titulo}</h2>
      {children}
    </section>
  );
}
