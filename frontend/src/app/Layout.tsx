import { Link, Outlet, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { Boton } from '@/shared/components/Boton';

export function Layout() {
  const { usuario, salir } = useSesion();
  const clienteQuery = useQueryClient();
  const navegar = useNavigate();

  const cerrarSesion = () => {
    salir();
    // Los datos en memoria son de la sesión que termina: no deben verse en la siguiente
    clienteQuery.clear();
    navegar('/login', { replace: true });
  };

  return (
    <div className='min-h-screen bg-tiza-fondo text-tiza-pizarra'>
      <header className='border-b border-tiza-pizarra/20 bg-white'>
        <div className='mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 p-4'>
          <div className='flex items-center gap-6'>
            <Link to='/' className='font-heading text-2xl font-black tracking-tight'>
              t<span className='inline-block -rotate-12 text-tiza-violeta'>ı</span>za
            </Link>
            {usuario?.rol !== 'alumno' && (
              <nav aria-label='Principal'>
                <Link to='/cursos' className='font-bold text-tiza-violeta underline'>
                  Cursos
                </Link>
              </nav>
            )}
          </div>
          <div className='flex items-center gap-3'>
            <span className='text-sm'>
              {usuario?.nombre} · <span className='capitalize'>{usuario?.rol}</span>
            </span>
            <Boton variante='secundario' onClick={cerrarSesion}>
              Salir
            </Boton>
          </div>
        </div>
      </header>
      <Outlet />
    </div>
  );
}
