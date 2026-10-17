import { useId, type InputHTMLAttributes } from 'react';

type Props = InputHTMLAttributes<HTMLInputElement> & {
  etiqueta: string;
  error?: string;
};

export function Campo({ etiqueta, error, className = '', id, ...resto }: Props) {
  const idGenerado = useId();
  const idCampo = id ?? idGenerado;
  const idError = `${idCampo}-error`;

  return (
    <div className='flex flex-col gap-1'>
      <label htmlFor={idCampo} className='font-bold text-tiza-pizarra'>
        {etiqueta}
      </label>
      <input
        {...resto}
        id={idCampo}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? idError : undefined}
        className={`rounded-md border px-3 py-2 text-tiza-pizarra disabled:cursor-not-allowed disabled:opacity-50 ${
          error ? 'border-tiza-coral' : 'border-tiza-pizarra'
        } ${className}`}
      />
      {error && (
        <p id={idError} role='alert' className='text-sm text-tiza-coral'>
          {error}
        </p>
      )}
    </div>
  );
}
