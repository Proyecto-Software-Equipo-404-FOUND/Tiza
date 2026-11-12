import { pedir } from '@/shared/api/cliente';

export type Docente = { id: string; nombre: string };

// Forma de UsuarioResumen en contracts/openapi/identidad-v1.json (GET /api/usuarios, S2-ID-10)
type UsuarioResumen = { id: string; nombre: string; apellido: string; email: string; rol: string };

// Docentes activos de la institución del token, por el gateway: /api/v1/identidad/usuarios
export async function listarDocentes(): Promise<Docente[]> {
  const usuarios = await pedir<UsuarioResumen[]>('/api/v1/identidad/usuarios?rol=Docente');
  return usuarios.map((u) => ({ id: u.id, nombre: `${u.nombre} ${u.apellido}`.trim() }));
}
