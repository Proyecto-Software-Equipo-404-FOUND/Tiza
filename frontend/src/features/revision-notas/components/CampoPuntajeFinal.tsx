import { Campo } from '@/shared/components/Campo';
import { formatearNumero } from '../formato';

type Props = {
  numero: number;
  maximo: number;
  sugerida: number | null | undefined;
  valor: string;
  error: string | null;
  deshabilitado: boolean;
  onCambiar: (valor: string) => void;
  /** Comentario interno del docente (el alumno no lo ve) */
  comentario?: string;
  onCambiarComentario?: (valor: string) => void;
};

// Campo donde el docente confirma o corrige el puntaje de una pregunta abierta
export function CampoPuntajeFinal({
  numero,
  maximo,
  sugerida,
  valor,
  error,
  deshabilitado,
  onCambiar,
  comentario,
  onCambiarComentario,
}: Props) {
  const idComentario = `comentario-pregunta-${numero}`;
  return (
    <div className='mt-4 flex flex-col gap-3'>
      <div className='max-w-xs'>
        <Campo
          etiqueta={`Nota final de la pregunta ${numero} (máximo ${formatearNumero(maximo)})`}
          inputMode='decimal'
          autoComplete='off'
          value={valor}
          disabled={deshabilitado}
          error={error ?? undefined}
          onChange={(evento) => onCambiar(evento.target.value)}
        />
        {sugerida != null && !deshabilitado && (
          <p className='mt-1 text-xs text-tiza-bosque/70'>La IA sugirió {formatearNumero(sugerida)}.</p>
        )}
      </div>
      {onCambiarComentario && (
        <div className='max-w-xl'>
          <label htmlFor={idComentario} className='text-sm font-bold text-tiza-bosque'>
            Comentario de la pregunta {numero} (opcional, el alumno no lo ve)
          </label>
          <textarea
            id={idComentario}
            className='mt-1 w-full rounded-xl border border-tiza-bosque/20 bg-white px-3 py-2 text-sm text-tiza-bosque disabled:bg-tiza-pizarra/5'
            rows={2}
            maxLength={1000}
            value={comentario ?? ''}
            disabled={deshabilitado}
            onChange={(evento) => onCambiarComentario(evento.target.value)}
          />
        </div>
      )}
    </div>
  );
}
