import { ShieldCheck } from 'lucide-react';
import { useId } from 'react';

// Texto fijo del criterio de aceptación de CU-04: no cambiarlo sin avisar al equipo
export const TEXTO_AVISO_INTEGRIDAD =
  'Durante el examen se registra si salís de esta pantalla. No se usa la cámara ni el micrófono.';

type Props = {
  aceptado: boolean;
  onCambiar: (aceptado: boolean) => void;
  /** Se muestra si se intentó entrar sin aceptar */
  error?: string;
  deshabilitado?: boolean;
};

// Aviso previo al examen. El alumno tiene que aceptarlo para poder entrar a la sala.
export function AvisoIntegridad({ aceptado, onCambiar, error, deshabilitado = false }: Props) {
  const id = useId();
  const idError = `${id}-error`;

  return (
    <section aria-labelledby={`${id}-titulo`} className='flex flex-col gap-3 rounded-xl bg-tiza-menta/40 p-4'>
      <div className='flex items-start gap-3'>
        <ShieldCheck className='mt-0.5 h-5 w-5 shrink-0 text-tiza-pasto' aria-hidden='true' />
        <div className='flex flex-col gap-1'>
          <h3 id={`${id}-titulo`} className='text-sm font-bold text-tiza-bosque'>
            Antes de empezar
          </h3>
          <p className='text-sm text-tiza-bosque/80'>{TEXTO_AVISO_INTEGRIDAD}</p>
          <p className='text-sm text-tiza-bosque/70'>
            Es solo un aviso para tu docente: no baja tu nota automáticamente.
          </p>
        </div>
      </div>

      <label htmlFor={id} className='flex min-h-11 cursor-pointer items-start gap-3 text-sm font-semibold text-tiza-bosque'>
        <input
          id={id}
          type='checkbox'
          checked={aceptado}
          disabled={deshabilitado}
          onChange={(evento) => onCambiar(evento.target.checked)}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? idError : undefined}
          className='mt-0.5 h-5 w-5 shrink-0 accent-tiza-pasto'
        />
        <span>Entendí el aviso y acepto</span>
      </label>
      {error && (
        <p id={idError} role='alert' className='text-sm text-tiza-coral'>
          {error}
        </p>
      )}
    </section>
  );
}
