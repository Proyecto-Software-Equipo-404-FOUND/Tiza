import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { guardarToken, obtenerToken, obtenerTokenRenovacion } from '@/shared/api/cliente';
import type { Usuario } from '@/shared/api/tipos';
import { cerrarSesionEnServidor } from './api/auth';
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
    // Con refreshToken, un accessToken vencido se renueva en el primer pedido
    if (tokenVencido(token) && !obtenerTokenRenovacion()) throw new Error('Token vencido');
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
    const tokenRenovacion = obtenerTokenRenovacion();
    // Se avisa al servidor sin esperar la respuesta: la sesión local se cierra igual
    if (tokenRenovacion) void cerrarSesionEnServidor(tokenRenovacion).catch(() => undefined);
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