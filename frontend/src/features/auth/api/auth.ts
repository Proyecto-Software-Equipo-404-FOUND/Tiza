import { guardarToken, pedir } from '@/shared/api/cliente';

// Respuesta real de Identidad (POST /api/auth/login, por el gateway: /api/v1/identidad/auth/login).
// No trae usuario: se obtiene del JWT. Los campos que hoy no se usan son opcionales.
export type RespuestaLogin = {
  accessToken: string;
  tokenType?: string;
  refreshToken?: string;
  accessTokenExpiresAt?: string;
  refreshTokenExpiresAt?: string;
};

export function iniciarSesion(correo: string, contrasena: string) {
  // Entrar es empezar una sesión nueva: se descarta un token viejo para que un 401 no redirija
  guardarToken(null);
  return pedir<RespuestaLogin>('/api/v1/identidad/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email: correo, password: contrasena }),
  });
}