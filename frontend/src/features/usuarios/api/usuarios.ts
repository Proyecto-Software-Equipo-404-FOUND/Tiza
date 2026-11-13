import { pedir } from '@/shared/api/cliente';

export type RolNuevo = 'Alumno' | 'Docente';

export type NuevoUsuario = { nombre: string; apellido: string; email: string; password: string; rol: RolNuevo };

// Respuesta de POST /api/auth/register (UsuarioRegistrado en contracts/openapi/identidad-v1.json)
export type UsuarioRegistrado = { id: string; message: string };

// Solo Administrador. El servidor crea el usuario en la institución del token; email repetido → 409.
// Por el gateway la ruta es /api/v1/identidad/auth/register.
export const registrarUsuario = (datos: NuevoUsuario) =>
  pedir<UsuarioRegistrado>('/api/v1/identidad/auth/register', {
    method: 'POST',
    body: JSON.stringify(datos),
  });

export type UsuarioDeLaLista = { id: string; nombre: string; email: string; rol: RolNuevo };

type UsuarioResumen = { id: string; nombre: string; apellido: string; email: string; rol: string };

// GET /api/usuarios?rol= (S2-ID-10): el mismo endpoint que usan la matrícula y el selector de docentes
export async function listarUsuarios(rol: RolNuevo): Promise<UsuarioDeLaLista[]> {
  const usuarios = await pedir<UsuarioResumen[]>(`/api/v1/identidad/usuarios?rol=${rol}`);
  return usuarios.map((u) => ({ id: u.id, nombre: `${u.nombre} ${u.apellido}`.trim(), email: u.email, rol }));
}
