import { Timer } from 'lucide-react';
import { useEffect, useState } from 'react';
import { formatearTiempo, segundosRestantes } from '../panel';

type Props = {
  etiqueta: string;
  limiteUtc: string | null | undefined;
  desfaseMs: number;
};

export function CronometroPanel({ etiqueta, limiteUtc, desfaseMs }: Props) {
  const calcular = () => segundosRestantes(limiteUtc, desfaseMs);
  const [restante, setRestante] = useState(calcular);

  useEffect(() => {
    setRestante(calcular());
    if (!limiteUtc) return;
    const intervalo = setInterval(() => setRestante(calcular()), 1000);
    return () => clearInterval(intervalo);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [limiteUtc, desfaseMs]);

  const sinFecha = restante === null;
  const agotado = restante !== null && restante <= 0;
  const texto = sinFecha ? '—' : agotado ? 'Tiempo agotado' : formatearTiempo(restante);

  return (
    <p
      role='timer'
      aria-label={`${etiqueta}: ${sinFecha ? 'sin fecha' : texto}`}
      className={`inline-flex items-center gap-2 rounded-full px-3 py-1 text-sm font-bold ${
        agotado ? 'bg-tiza-coral text-white' : 'bg-tiza-menta text-tiza-pasto'
      }`}
    >
      <Timer className='h-4 w-4' aria-hidden='true' />
      <span>{etiqueta}</span>
      <span>{texto}</span>
    </p>
  );
}