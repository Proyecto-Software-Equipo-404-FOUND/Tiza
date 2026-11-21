import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';

/** Cuando un pedido del invitado devuelve 401: el token dura 4 h y no tiene refresh */
export const MENSAJE_ACCESO_VENCIDO = 'Tu acceso como invitado venció. Volvé a ingresar con el PIN y tu nombre.';

// El código del error viaja en `type` (contrato identidad-v1, POST /api/auth/invitado)
const MENSAJES = new Map<string, string>([
  ['NOMBRE_INVALIDO', 'El nombre tiene que tener entre 2 y 60 caracteres.'],
  ['SALA_SIN_INVITADOS', 'Esta sala no admite invitados. Pedile al docente que te habilite.'],
  ['SALA_NO_ENCONTRADA', 'No hay una sala activa con ese PIN. Revisá el número e intentá de nuevo.'],
  ['SALA_FINALIZADA', 'La sala ya finalizó.'],
  ['SALA_NO_DISPONIBLE', 'No pudimos verificar la sala. Probá de nuevo.'],
]);

export function mensajeIngresoInvitado(error: unknown): string {
  if (error instanceof ApiError) {
    const mensaje = MENSAJES.get(error.detalle?.type ?? '');
    if (mensaje) return mensaje;
    // El 503 no está en el contrato de Identidad todavía: se cubre por estado
    if (error.estado === 503) return MENSAJES.get('SALA_NO_DISPONIBLE') ?? mensajeDeError(error);
  }
  return mensajeDeError(error);
}