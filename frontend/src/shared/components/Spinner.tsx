import { Loader2 } from 'lucide-react';

export function Spinner({ texto = 'Cargando…' }: { texto?: string }) {
  return (
    <div role='status' className='flex items-center gap-2 text-tiza-pizarra'>
      <Loader2 className='h-5 w-5 animate-spin' aria-hidden='true' />
      <span>{texto}</span>
    </div>
  );
}
