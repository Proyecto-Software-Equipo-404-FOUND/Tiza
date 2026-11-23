import { Navigate, Outlet } from 'react-router-dom';
import { useSesion } from '@/features/auth/SesionContext';
import type { Rol } from '@/shared/api/tipos';

type Props = {
  // Sin roles, alcanza con haber iniciado sesión
  roles?: Rol[];
};

export function RutaProtegida({ roles }: Props) {
  const { usuario, invitadoVencido } = useSesion();

  if (!usuario) {
    // Un invitado cuyo acceso venció vuelve a su ingreso (no tiene cuenta con la que entrar por el login)
    return <Navigate to={invitadoVencido ? '/invitado?vencido=1' : '/login'} replace />;
  }
  if (roles && !roles.includes(usuario.rol)) {
    return <Navigate to='/no-autorizado' replace />;
  }
  return <Outlet />;
}