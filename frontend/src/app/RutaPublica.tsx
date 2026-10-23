import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';
import { rutaInicial } from '@/features/auth/rutaInicial';
import { useSesion } from '@/features/auth/SesionContext';

// Para /login: quien ya tiene sesión no necesita ver el formulario de nuevo
export function RutaPublica({ children }: { children: ReactNode }) {
  const { usuario } = useSesion();
  if (usuario) return <Navigate to={rutaInicial(usuario.rol)} replace />;
  return <>{children}</>;
}
