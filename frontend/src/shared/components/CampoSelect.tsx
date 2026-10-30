import { useId, type ReactNode, type SelectHTMLAttributes } from 'react';

type Props = SelectHTMLAttributes<HTMLSelectElement> & {
  etiqueta: string;
  error?: string;
  /** Ícono decorativo a la izquierda del texto */
  icono?: ReactNode;
};

// Lista desplegable con el mismo aspecto que Campo (etiqueta, ícono y error)
export function CampoSelect({ etiqueta, error, icono, className = '', id, children, ...resto }: Props) {
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
        <select
          {...resto}
          id={idCampo}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? idError : undefined}
          className={`w-full rounded-xl border bg-white py-3 pr-4 text-tiza-bosque disabled:cursor-not-allowed disabled:opacity-50 ${
            icono ? 'pl-11' : 'pl-4'
          } ${error ? 'border-tiza-coral' : 'border-tiza-borde'} ${className}`}
        >
          {children}
        </select>
      </div>
      {error && (
        <p id={idError} role='alert' className='text-sm text-tiza-coral'>
          {error}
        </p>
      )}
    </div>
  );
}
