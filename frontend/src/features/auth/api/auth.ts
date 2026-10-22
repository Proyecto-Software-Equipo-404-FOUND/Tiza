import { guardarToken, pedir } from '@/shared/api/cliente';

// Provisorio: la ruta, el cuerpo y la respuesta del login los define Identidad.
export type RespuestaLogin = { accessToken: string; tokenType?: string };

export function iniciarSesion(correo: string, contrasena: string) {
  // Entrar es empezar una sesión nueva: se descarta un token viejo para que un 401 no redirija
  guardarToken(null);
  return pedir<RespuestaLogin>('/api/v1/identidad/sesiones', {
    method: 'POST',
    body: JSON.stringify({ email: correo, password: contrasena }),
  });
}
