import { obtenerTokenVigente } from '@/shared/api/cliente';
import { crearConexion } from '@/shared/realtime/conexion';
import { conexionReal } from '@/shared/realtime/conexionReal';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { crearConexionDemoDocente } from '@/mocks/conexionDemoDocente';

// Con VITE_USE_MOCKS=true se usa una sala de mentira para poder probar el panel sin backend.
// obtenerTokenVigente renueva el accessToken si está por vencer, así la conexión nunca se reabre con uno vencido.
export function crearConexionSala(): ConexionSala {
  if (import.meta.env.VITE_USE_MOCKS === 'true') return crearConexionDemoDocente();
  return conexionReal(crearConexion(obtenerTokenVigente));
}