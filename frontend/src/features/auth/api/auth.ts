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

// Avisa al servidor que el refreshToken ya no se usa (POST /api/auth/logout, responde 204)
export function cerrarSesionEnServidor(refreshToken: string) {
  return pedir<void>('/api/v1/identidad/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
  });
}

// S3-ID-03: el usuario cambia su propia contraseña (POST /api/auth/cambiar-contrasena, responde 204).
// El servidor revoca todos los refresh tokens: después hay que volver a ingresar.
export function cambiarContrasena(contrasenaActual: string, contrasenaNueva: string) {
  return pedir<void>('/api/v1/identidad/auth/cambiar-contrasena', {
    method: 'POST',
    body: JSON.stringify({ contrasenaActual, contrasenaNueva }),
  });
}
