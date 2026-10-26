import { Home, GraduationCap } from 'lucide-react';
import type { ReactNode } from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { Boton } from '@/shared/components/Boton';
import { Logo } from '@/shared/components/Logo';

function ItemMenu({ to, end, icono, children }: { to: string; end?: boolean; icono: ReactNode; children: ReactNode }) {
  return (
    <NavLink
      to={to}
      end={end}
      className={({ isActive }) =>
        `flex items-center gap-3 rounded-xl px-4 py-3 font-semibold transition ${
          isActive ? 'bg-tiza-menta text-tiza-bosque' : 'text-tiza-bosque/80 hover:bg-tiza-menta/60'
        }`
      }
    >
      {icono}
      {children}
    </NavLink>
  );
}

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
    <div className='min-h-screen bg-tiza-fondo text-tiza-bosque'>
      <header className='border-b border-tiza-borde bg-white'>
        <div className='flex flex-wrap items-center justify-between gap-3 px-6 py-3'>
          <Link to='/' aria-label='tiza, ir al inicio'>
            <Logo />
          </Link>
          <div className='flex items-center gap-3'>
            <span className='text-sm'>
              {usuario?.nombre} · <span className='capitalize'>{usuario?.rol}</span>
            </span>
            <Boton variante='secundario' onClick={cerrarSesion} className='!py-2'>
              Salir
            </Boton>
          </div>
        </div>
      </header>
      <div className='flex flex-col md:flex-row'>
        <nav
          aria-label='Principal'
          className='flex gap-2 border-b border-tiza-borde bg-white/60 p-3 md:min-h-[calc(100vh-4.5rem)] md:w-60 md:shrink-0 md:flex-col md:border-b-0 md:border-r md:p-4'
        >
          <ItemMenu to='/' end icono={<Home className='h-5 w-5' aria-hidden='true' />}>
            Inicio
          </ItemMenu>
          {usuario?.rol !== 'alumno' && (
            <ItemMenu to='/cursos' icono={<GraduationCap className='h-5 w-5' aria-hidden='true' />}>
              Cursos
            </ItemMenu>
          )}
        </nav>
        <div className='min-w-0 flex-1'>
          <Outlet />
        </div>
      </div>
    </div>
  );
}
