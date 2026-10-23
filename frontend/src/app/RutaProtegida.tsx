import { Navigate, Outlet } from 'react-router-dom';
import { useSesion } from '@/features/auth/SesionContext';
import type { Rol } from '@/shared/api/tipos';

type Props = {
  // Sin `roles`, alcanza con haber iniciado sesión
  roles?: Rol[];
};

export function RutaProtegida({ roles }: Props) {
  const { usuario } = useSesion();

  if (!usuario) {
    return <Navigate to='/login' replace />;
  }
  if (roles && !roles.includes(usuario.rol)) {
    return <Navigate to='/no-autorizado' replace />;
  }
  return <Outlet />;
}
