import { Boton } from '@/shared/components/Boton';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { formatearFechaHora, formatearNota } from '../formato';
import type { ResumenNota } from '../notaFinal';

type Props = {
  resumen: ResumenNota;
  aprobada: boolean;
  /** Solo el docente dueño del examen edita y aprueba; el administrador la ve sin botón */
  soloLectura: boolean;
  /** Nota final ya guardada (cuando está aprobada) */
  notaAprobada: number | null | undefined;
  /** Cuándo se aprobó; si no viene, no se muestra */
  fechaAprobacionUtc?: string | null;
  hayQueEditar: boolean;
  cargando: boolean;
  error: string | null;
  onAprobar: () => void;
};

// Resumen de la nota final de una entrega y botón para aprobarla
export function TarjetaNotaFinal({ resumen, aprobada, soloLectura, notaAprobada, fechaAprobacionUtc, hayQueEditar, cargando, error, onAprobar }: Props) {
  const total = aprobada ? (notaAprobada ?? resumen.total) : resumen.total;

  return (
    <Tarjeta
      titulo='Nota final'
      subtitulo={
        aprobada
          ? `Nota corregida${fechaAprobacionUtc ? ` (${formatearFechaHora(fechaAprobacionUtc)})` : ''}. El alumno ya puede verla.`
          : soloLectura
            ? 'Todavía no está corregida. Solo el docente que creó el examen puede aprobarla.'
            : 'Revisá la nota que sugirió la IA, corregila si hace falta y aprobala. Hasta entonces el alumno no la ve.'
      }
    >
      <div className='flex flex-wrap items-center justify-between gap-4'>
        <p className='font-heading text-3xl font-extrabold text-tiza-bosque' aria-live='polite'>
          {total == null ? 'Faltan puntajes' : formatearNota(total, resumen.maximo)}
        </p>
        {aprobada ? (
          <span className='rounded-full bg-tiza-menta px-4 py-2 text-sm font-bold text-tiza-bosque'>Corregida</span>
        ) : soloLectura ? null : (
          <Boton type='button' onClick={onAprobar} disabled={resumen.total == null} cargando={cargando}>
            Aprobar nota final
          </Boton>
        )}
      </div>
      {!aprobada && !soloLectura && resumen.total == null && hayQueEditar && (
        <p className='mt-3 text-sm text-tiza-bosque/80'>
          {resumen.conProblemas === 1
            ? 'Hay 1 pregunta sin puntaje válido.'
            : `Hay ${resumen.conProblemas} preguntas sin puntaje válido.`}{' '}
          Completalas para poder aprobar.
        </p>
      )}
      {error && (
        <p role='alert' className='mt-3 text-sm font-semibold text-tiza-coral'>
          {error}
        </p>
      )}
    </Tarjeta>
  );
}
