type Props = { className?: string; tamano?: 'normal' | 'grande' };

// Tiza + palabra "tiza". Es un SVG propio, sin archivos externos.
// Es una barra de tiza (cilindro blanco con la punta tallada) inclinada. El recuadro (viewBox) está
// recortado justo al dibujo para que el ícono se alinee y se pegue bien al texto.
export function IconoTiza({ className = 'h-8 w-auto' }: { className?: string }) {
  return (
    <svg viewBox='5.3 -0.1 23.2 26.2' className={className} aria-hidden='true' focusable='false'>
      <g transform='translate(0 -2) rotate(38 16 14)' strokeLinejoin='round' strokeLinecap='round'>
        <path d='M11 5 V19 Q11 20 11.7 21 L14 24.6 Q14.8 25.9 16.1 25.5 L19.9 24.1 Q21 23.7 21 22.6 V5 Z' fill='#F3F8F6' stroke='#2F7D5B' strokeWidth='1.4' />
        <path d='M11.8 19.4 Q11.8 20 12.4 20.9 L14.6 24.2 Q15.1 24.9 15.9 24.6 L19.5 23.3 Q20.2 23 19.9 22.4 L16.6 20 Z' fill='#DCEBE4' />
        <ellipse cx='16' cy='5' rx='5' ry='2.3' fill='#BFE5D3' stroke='#2F7D5B' strokeWidth='1.4' />
        <path d='M13.2 10.5 V13.5 M13.2 16 V17.5' stroke='#BFE5D3' strokeWidth='1.5' fill='none' />
      </g>
    </svg>
  );
}

export function Logo({ className = '', tamano = 'normal' }: Props) {
  const grande = tamano === 'grande';
  return (
    <span className={`inline-flex items-center gap-1.5 font-heading font-extrabold text-tiza-bosque ${className}`}>
      <IconoTiza className={grande ? 'h-12 w-auto' : 'h-8 w-auto'} />
      <span className={grande ? 'text-6xl leading-none tracking-tight' : 'text-3xl leading-none tracking-tight'}>tiza</span>
    </span>
  );
}
