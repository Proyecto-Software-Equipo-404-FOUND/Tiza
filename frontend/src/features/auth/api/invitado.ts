import { guardarToken, pedir } from '@/shared/api/cliente';

// Respuesta de POST /api/auth/invitado, por el gateway: /api/v1/identidad/auth/invitado (contrato identidad-v1).
// Token de rol Invitado, válido solo para la sala del PIN y sin refresh token.
export type SesionInvitado = {
  accessToken: string;
  /** 4 horas desde el ingreso, sin importar cuándo termine la sala */
  expiraEnUtc: string;
  salaId: string;
  nombre: string;
};

export function ingresarComoInvitado(pin: number, nombre: string) {
  // Entrar es empezar una sesión nueva: se descarta cualquier token anterior (y su refresh) de esta pestaña
  guardarToken(null);
  return pedir<SesionInvitado>('/api/v1/identidad/auth/invitado', {
    method: 'POST',
    body: JSON.stringify({ pin, nombre }),
  });
}
