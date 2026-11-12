import type { ReactNode } from 'react';
import type { AlertasFocoResumen, EstadoEntrega } from '../tipos';
import { etiquetaEstadoEntrega } from '../formato';

const estilosEstado: Record<EstadoEntrega, string> = {
  Corregida: 'bg-tiza-menta text-tiza-pasto',
  PendienteCorreccion: 'bg-tiza-lima/40 text-tiza-bosque',
  CorreccionManual: 'bg-tiza-pizarra/10 text-tiza-pizarra',
};

export function InsigniaEstadoEntrega({ estado }: { estado: EstadoEntrega }) {
  return (
    <span className={`inline-block rounded-full px-3 py-1 text-xs font-bold ${estilosEstado[estado]}`}>
      {etiquetaEstadoEntrega[estado]}
    </span>
  );
}

/** Etiqueta que deja claro que la nota de la IA no es la nota final (RN-CMP-03). */
export function EtiquetaSugerida() {
  return (
    <span className='inline-block rounded-full bg-tiza-violeta/10 px-2 py-0.5 text-xs font-bold text-tiza-violeta'>
      Sugerida
    </span>
  );
}

const insigniaGravedad = 'rounded-full border px-2.5 py-0.5 text-xs font-bold text-tiza-bosque';

/** Roja: coral. Amarilla: lima. El texto lleva siempre el nombre de la gravedad, el color solo acompaña. */
export function InsigniaGravedad({ roja, children }: { roja: boolean; children: ReactNode }) {
  const color = roja ? 'border-tiza-coral bg-tiza-coral/15' : 'border-tiza-lima bg-tiza-lima/40';
  return <span className={`${insigniaGravedad} ${color}`}>{children}</span>;
}

const plural = (n: number, uno: string, varios: string) => `${n} ${n === 1 ? uno : varios}`;

/** Alertas de foco como aviso para el docente: no cambian la nota ni el orden (RN-CMP-07). */
export function AlertasFoco({ alertas }: { alertas: AlertasFocoResumen }) {
  if (alertas.amarillas === 0 && alertas.rojas === 0) {
    return <span className='text-tiza-bosque/70'>Sin alertas</span>;
  }
  return (
    <span className='flex flex-wrap gap-1.5'>
      {alertas.rojas > 0 && <InsigniaGravedad roja>{plural(alertas.rojas, 'roja', 'rojas')}</InsigniaGravedad>}
      {alertas.amarillas > 0 && <InsigniaGravedad roja={false}>{plural(alertas.amarillas, 'amarilla', 'amarillas')}</InsigniaGravedad>}
    </span>
  );
}
