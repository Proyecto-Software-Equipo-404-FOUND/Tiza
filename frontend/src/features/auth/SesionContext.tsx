import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { guardarToken, obtenerToken } from '@/shared/api/cliente';
import type { Usuario } from '@/shared/api/tipos';
import { tokenVencido, usuarioDesdeToken } from './jwt';

type Sesion = {
  usuario: Usuario | null;
  entrar: (token: string, usuario?: Usuario) => Usuario;
  salir: () => void;
};

const SesionContext = createContext<Sesion | null>(null);

// Al recargar la página el token sigue en sessionStorage: de ahí se recupera el usuario
function recuperarUsuario(): Usuario | null {
  const token = obtenerToken();
  if (!token) return null;
  try {
    if (tokenVencido(token)) throw new Error('Token vencido');
    return usuarioDesdeToken(token);
  } catch {
    guardarToken(null);
    return null;
  }
}

export function SesionProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(recuperarUsuario);

  const entrar = useCallback((token: string, nuevo?: Usuario) => {
    const datos = nuevo ?? usuarioDesdeToken(token);
    guardarToken(token);
    setUsuario(datos);
    return datos;
  }, []);

  const salir = useCallback(() => {
    guardarToken(null);
    setUsuario(null);
  }, []);

  const valor = useMemo(() => ({ usuario, entrar, salir }), [usuario, entrar, salir]);

  return <SesionContext.Provider value={valor}>{children}</SesionContext.Provider>;
}

export function useSesion() {
  const contexto = useContext(SesionContext);
  if (!contexto) throw new Error('useSesion debe usarse dentro de SesionProvider');
  return contexto;
}
