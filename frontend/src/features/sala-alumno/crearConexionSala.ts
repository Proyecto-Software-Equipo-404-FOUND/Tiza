import { obtenerToken } from '@/shared/api/cliente';
import { crearConexion } from '@/shared/realtime/conexion';
import { conexionReal } from '@/shared/realtime/conexionReal';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { crearConexionDemo } from '@/mocks/conexionDemo';

// Con VITE_USE_MOCKS=true se usa una sala de mentira para poder probar la pantalla sin backend
export function crearConexionSala(): ConexionSala {
  if (import.meta.env.VITE_USE_MOCKS === 'true') return crearConexionDemo();
  return conexionReal(crearConexion(obtenerToken));
}
