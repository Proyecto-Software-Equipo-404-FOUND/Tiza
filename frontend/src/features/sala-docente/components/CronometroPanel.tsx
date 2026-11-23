import { Timer } from 'lucide-react';
import { useEffect, useState } from 'react';
import { formatearTiempo, segundosRestantes } from '../panel';

type Props = {
  etiqueta: string;
  limiteUtc: string | null | undefined;
  desfaseMs: number;
  /** Duración que se muestra quieta (la sala todavía no empezó): no cuenta ni se marca como agotada */
  fijoSegundos?: number;
};

export function CronometroPanel({ etiqueta, limiteUtc, desfaseMs, fijoSegundos }: Props) {
  const calcular = () => (fijoSegundos !== undefined ? fijoSegundos : segundosRestantes(limiteUtc, desfaseMs));
  const [restante, setRestante] = useState(calcular);

  useEffect(() => {
    setRestante(calcular());
    if (fijoSegundos !== undefined || !limiteUtc) return;
    const intervalo = setInterval(() => setRestante(calcular()), 1000);
    return () => clearInterval(intervalo);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [limiteUtc, desfaseMs, fijoSegundos]);

  const sinFecha = restante === null;
  const agotado = fijoSegundos === undefined && restante !== null && restante <= 0;
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