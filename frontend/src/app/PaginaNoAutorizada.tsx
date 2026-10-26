import { Link } from 'react-router-dom';
import { rutaInicial } from '@/features/auth/rutaInicial';
import { useSesion } from '@/features/auth/SesionContext';
import { Tarjeta } from '@/shared/components/Tarjeta';

export function PaginaNoAutorizada() {
  const { usuario } = useSesion();
  return (
    <main className='mx-auto max-w-md p-4'>
      <Tarjeta titulo='Sin permiso'>
        <p role='alert' className='mb-4'>
          No tenés permiso para ver esta página.
        </p>
        <Link
          to={usuario ? rutaInicial(usuario.rol) : '/login'}
          className='font-bold text-tiza-pasto underline'
        >
          {usuario ? 'Volver al inicio' : 'Ir a ingresar'}
        </Link>
      </Tarjeta>
    </main>
  );
}
