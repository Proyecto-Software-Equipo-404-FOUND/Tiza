type Props = { className?: string; tamano?: 'normal' | 'grande' };

// Lápiz verde + palabra "tiza". Es un SVG propio, sin archivos externos.
export function IconoLapiz({ className = 'h-8 w-8' }: { className?: string }) {
  return (
    <svg viewBox='0 0 32 32' className={className} aria-hidden='true' focusable='false'>
      <g transform='rotate(45 16 16)'>
        <rect x='12' y='1' width='8' height='24' rx='4' fill='#7CC24A' />
        <path d='M12 22 L16 31 L20 22 Z' fill='#2F7D5B' />
        <rect x='12' y='1' width='8' height='6' rx='3' fill='#2F7D5B' />
      </g>
    </svg>
  );
}

export function Logo({ className = '', tamano = 'normal' }: Props) {
  const grande = tamano === 'grande';
  return (
    <span className={`inline-flex items-center gap-2 font-heading font-extrabold text-tiza-bosque ${className}`}>
      <IconoLapiz className={grande ? 'h-12 w-12' : 'h-8 w-8'} />
      <span className={grande ? 'text-6xl tracking-tight' : 'text-3xl tracking-tight'}>tiza</span>
    </span>
  );
}
