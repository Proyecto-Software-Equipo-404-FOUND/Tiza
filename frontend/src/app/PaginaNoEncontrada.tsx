import { Link } from 'react-router-dom';
import { Tarjeta } from '@/shared/components/Tarjeta';

export function PaginaNoEncontrada() {
  return (
    <main className='mx-auto max-w-md p-4'>
      <Tarjeta titulo='No encontrada'>
        <p className='mb-4'>No se encontró lo que buscás.</p>
        <Link to='/' className='font-bold text-tiza-violeta underline'>
          Volver al inicio
        </Link>
      </Tarjeta>
    </main>
  );
}
