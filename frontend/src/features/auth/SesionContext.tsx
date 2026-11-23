import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { guardarToken, obtenerToken, obtenerTokenRenovacion } from '@/shared/api/cliente';
import type { Usuario } from '@/shared/api/tipos';
import { cerrarSesionEnServidor } from './api/auth';
import { tokenVencido, usuarioDesdeToken } from './jwt';

type Sesion = {
  usuario: Usuario | null;
  /** El token de un invitado venció (dura 4 h y no tiene refresh): se lo manda a su pantalla de ingreso, no al login */
  invitadoVencido: boolean;
  entrar: (token: string, usuario?: Usuario) => Usuario;
  salir: () => void;
};

type Estado = { usuario: Usuario | null; invitadoVencido: boolean };

const SIN_SESION: Estado = { usuario: null, invitadoVencido: false };

const SesionContext = createContext<Sesion | null>(null);

// Al recargar la página el token sigue en sessionStorage: de ahí se recupera el usuario
function recuperarSesion(): Estado {
  const token = obtenerToken();
  if (!token) return SIN_SESION;
  try {
    // Con refreshToken, un accessToken vencido se renueva en el primer pedido
    if (tokenVencido(token) && !obtenerTokenRenovacion()) {
      // El invitado no tiene refresh: vuelve a su ingreso. No se limpia el token acá (este inicializador puede
      // correr dos veces en desarrollo): ingresarComoInvitado e iniciarSesion lo descartan antes de pedir.
      if (usuarioDesdeToken(token).rol === 'invitado') return { usuario: null, invitadoVencido: true };
      throw new Error('Token vencido');
    }
    return { usuario: usuarioDesdeToken(token), invitadoVencido: false };
  } catch {
    guardarToken(null);
    return SIN_SESION;
  }
}

export function SesionProvider({ children }: { children: ReactNode }) {
  const [estado, setEstado] = useState<Estado>(recuperarSesion);

  const entrar = useCallback((token: string, nuevo?: Usuario) => {
    const datos = nuevo ?? usuarioDesdeToken(token);
    guardarToken(token);
    setEstado({ usuario: datos, invitadoVencido: false });
    return datos;
  }, []);

  const salir = useCallback(() => {
    const tokenRenovacion = obtenerTokenRenovacion();
    // Se avisa al servidor sin esperar la respuesta: la sesión local se cierra igual
    if (tokenRenovacion) void cerrarSesionEnServidor(tokenRenovacion).catch(() => undefined);
    guardarToken(null);
    setEstado(SIN_SESION);
  }, []);

  const valor = useMemo(
    () => ({ usuario: estado.usuario, invitadoVencido: estado.invitadoVencido, entrar, salir }),
    [estado, entrar, salir],
  );

  return <SesionContext.Provider value={valor}>{children}</SesionContext.Provider>;
}

export function useSesion() {
  const contexto = useContext(SesionContext);
  if (!contexto) throw new Error('useSesion debe usarse dentro de SesionProvider');
  return contexto;
}