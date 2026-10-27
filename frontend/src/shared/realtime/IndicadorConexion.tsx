import { BannerError } from '../components/BannerError';
import { Spinner } from '../components/Spinner';
import type { EstadoConexion } from './tipos';

type Props = {
  estado: EstadoConexion;
  onReintentar?: () => void;
};

export function IndicadorConexion({ estado, onReintentar }: Props) {
  if (estado === 'conectando') return <Spinner texto='Conectando…' />;

  if (estado === 'conectado') {
    return (
      <p role='status' className='text-sm text-tiza-bosque'>
        {/* Verde a propósito: la guía (sección 10.4) pide un detalle discreto en verde para «Conectado» */}
        <span aria-hidden='true' className='mr-2 inline-block h-2 w-2 rounded-full bg-green-600' />
        Conectado
      </p>
    );
  }

  if (estado === 'reconectando') {
    return (
      <p role='alert' className='rounded-xl bg-tiza-coral px-3 py-2 font-bold text-white'>
        Se cortó la conexión. Reconectando…
      </p>
    );
  }

  return <BannerError mensaje='Se perdió la conexión con la sala.' onReintentar={onReintentar} />;
}
