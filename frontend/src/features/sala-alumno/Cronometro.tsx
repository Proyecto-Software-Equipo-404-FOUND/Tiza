import { Timer } from 'lucide-react';
import { useEffect, useState } from 'react';

/** Milisegundos que faltan para `limiteUtc`, usando el reloj del servidor (la fuente de verdad). */
export function useTiempoRestante(limiteUtc: string, desfaseMs: number) {
  const calcular = () => new Date(limiteUtc).getTime() - (Date.now() + desfaseMs);
  const [restante, setRestante] = useState(calcular);

  useEffect(() => {
    setRestante(calcular());
    const intervalo = setInterval(() => setRestante(calcular()), 1000);
    return () => clearInterval(intervalo);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [limiteUtc, desfaseMs]);

  return Math.max(0, restante);
}

export const formatearTiempo = (ms: number) => {
  const totalSegundos = Math.ceil(ms / 1000);
  const minutos = Math.floor(totalSegundos / 60);
  const segundos = totalSegundos % 60;
  return `${String(minutos).padStart(2, '0')}:${String(segundos).padStart(2, '0')}`;
};

type Props = { limiteUtc: string; desfaseMs: number };

export function Cronometro({ limiteUtc, desfaseMs }: Props) {
  const restante = useTiempoRestante(limiteUtc, desfaseMs);
  const agotado = restante <= 0;

  return (
    <p
      role='timer'
      aria-label={agotado ? 'Tiempo agotado' : `Tiempo restante ${formatearTiempo(restante)}`}
      className={`inline-flex items-center gap-2 rounded-full px-3 py-1 text-sm font-bold ${
        agotado ? 'bg-tiza-coral text-white' : 'bg-tiza-menta text-tiza-pasto'
      }`}
    >
      <Timer className='h-4 w-4' aria-hidden='true' />
      {agotado ? 'Tiempo agotado' : formatearTiempo(restante)}
    </p>
  );
}
