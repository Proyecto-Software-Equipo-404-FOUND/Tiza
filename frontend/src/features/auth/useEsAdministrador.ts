import { useSesion } from './SesionContext';

// Según el contrato de Cursos, crear, editar y borrar cursos y cohortes es solo del Administrador
// (el docente solo consulta). Se usa para no ofrecer acciones que el servidor rechaza con 403.
export function useEsAdministrador(): boolean {
  return useSesion().usuario?.rol === 'administrador';
}
