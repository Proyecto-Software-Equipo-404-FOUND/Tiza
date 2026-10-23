import { useRouteError } from 'react-router-dom';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';

// Red de seguridad del router: si una pantalla se rompe, no queda la pantalla en blanco
export function PaginaErrorRuta() {
  const error = useRouteError();
  return (
    <main className='mx-auto max-w-md p-4'>
      <BannerError mensaje={mensajeDeError(error)} onReintentar={() => window.location.reload()} />
    </main>
  );
}
