import { useId, type InputHTMLAttributes, type ReactNode } from 'react';

type Props = InputHTMLAttributes<HTMLInputElement> & {
  etiqueta: string;
  error?: string;
  /** Ícono decorativo a la izquierda del texto */
  icono?: ReactNode;
  /** Elemento a la derecha (por ejemplo, el botón de mostrar contraseña) */
  accion?: ReactNode;
};

export function Campo({ etiqueta, error, icono, accion, className = '', id, ...resto }: Props) {
  const idGenerado = useId();
  const idCampo = id ?? idGenerado;
  const idError = `${idCampo}-error`;

  return (
    <div className='flex flex-col gap-1.5'>
      <label htmlFor={idCampo} className='text-sm font-semibold text-tiza-bosque'>
        {etiqueta}
      </label>
      <div className='relative'>
        {icono && (
          <span className='pointer-events-none absolute inset-y-0 left-3 flex items-center text-tiza-bosque/70'>
            {icono}
          </span>
        )}
        <input
          {...resto}
          id={idCampo}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? idError : undefined}
          className={`w-full rounded-xl border bg-white py-3 text-tiza-bosque placeholder:text-tiza-bosque/40 disabled:cursor-not-allowed disabled:opacity-50 ${
            icono ? 'pl-11' : 'pl-4'
          } ${accion ? 'pr-12' : 'pr-4'} ${error ? 'border-tiza-coral' : 'border-tiza-borde'} ${className}`}
        />
        {accion && <span className='absolute inset-y-0 right-3 flex items-center'>{accion}</span>}
      </div>
      {error && (
        <p id={idError} role='alert' className='text-sm text-tiza-coral'>
          {error}
        </p>
      )}
    </div>
  );
}
