import type { EstadoEvaluacion } from '../tipos';

const estilos: Record<EstadoEvaluacion, string> = {
  Publicada: 'bg-tiza-menta text-tiza-pasto',
  Borrador: 'bg-amber-100 text-amber-700',
};

export function InsigniaEstado({ estado }: { estado: EstadoEvaluacion }) {
  return (
    <span className={`inline-block rounded-full px-3 py-1 text-xs font-bold ${estilos[estado]}`}>{estado}</span>
  );
}
