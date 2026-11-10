import type { AlertasFocoResumen, EstadoEntrega } from '../tipos';
import { etiquetaEstadoEntrega } from '../formato';

const estilosEstado: Record<EstadoEntrega, string> = {
  Corregida: 'bg-tiza-menta text-tiza-pasto',
  PendienteCorreccion: 'bg-amber-100 text-amber-700',
  CorreccionManual: 'bg-sky-100 text-sky-700',
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
    <span className='inline-block rounded-full bg-violet-100 px-2 py-0.5 text-xs font-bold text-violet-700'>
      Sugerida
    </span>
  );
}

const plural = (n: number, uno: string, varios: string) => `${n} ${n === 1 ? uno : varios}`;

/** Alertas de foco como aviso para el docente: no cambian la nota ni el orden (RN-CMP-07). */
export function AlertasFoco({ alertas }: { alertas: AlertasFocoResumen }) {
  if (alertas.amarillas === 0 && alertas.rojas === 0) {
    return <span className='text-tiza-bosque/70'>Sin alertas</span>;
  }
  return (
    <span className='flex flex-wrap gap-1.5'>
      {alertas.rojas > 0 && (
        <span className='rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-bold text-red-700'>
          {plural(alertas.rojas, 'roja', 'rojas')}
        </span>
      )}
      {alertas.amarillas > 0 && (
        <span className='rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-700'>
          {plural(alertas.amarillas, 'amarilla', 'amarillas')}
        </span>
      )}
    </span>
  );
}
