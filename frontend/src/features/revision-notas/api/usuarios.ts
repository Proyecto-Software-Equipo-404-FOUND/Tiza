import { pedir } from '@/shared/api/cliente';
import type { UsuarioResumen } from '../tipos';

// Por el gateway, /api/v1/identidad/usuarios llega a Identidad como /api/usuarios.
// Devuelve solo usuarios activos de la institución del token; Alumno → 403.
export const listarAlumnos = () => pedir<UsuarioResumen[]>('/api/v1/identidad/usuarios?rol=Alumno');
