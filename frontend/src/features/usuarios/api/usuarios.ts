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

export type UsuarioDeLaLista = {
  id: string;
  nombre: string;
  email: string;
  rol: RolNuevo;
  activo?: boolean;
  /** Importado sin contraseña inicial y todavía sin activar: no se puede reactivar a mano */
  pendienteActivacion?: boolean;
};

type UsuarioResumen = {
  id: string;
  nombre: string;
  apellido: string;
  email: string;
  rol: string;
  activo?: boolean;
  pendienteActivacion?: boolean;
};

// GET /api/usuarios?rol= (S2-ID-10): el mismo endpoint que usan la matrícula y el selector de docentes.
// Con incluirInactivos (S3-ID-01, solo administrador) también vienen los dados de baja, para reactivarlos.
export async function listarUsuarios(rol: RolNuevo, incluirInactivos = false): Promise<UsuarioDeLaLista[]> {
  const usuarios = await pedir<UsuarioResumen[]>(
    `/api/v1/identidad/usuarios?rol=${rol}${incluirInactivos ? '&incluirInactivos=true' : ''}`,
  );
  return usuarios.map((u) => ({
    id: u.id,
    nombre: `${u.nombre} ${u.apellido}`.trim(),
    email: u.email,
    rol,
    ...(incluirInactivos ? { activo: u.activo !== false, pendienteActivacion: u.pendienteActivacion === true } : {}),
  }));
}

// S3-ID-01 (#243): baja y reactivación. Solo Administrador; 204 sin cuerpo. No borra datos.
export const darDeBajaUsuario = (id: string) =>
  pedir<void>(`/api/v1/identidad/usuarios/${id}/baja`, { method: 'POST' });

export const reactivarUsuario = (id: string) =>
  pedir<void>(`/api/v1/identidad/usuarios/${id}/reactivar`, { method: 'POST' });

// Respuesta de POST /api/usuarios/alumnos/importacion (ResultadoImportacion, S2-ID-02)
export type ErrorDeFila = { fila: number; motivo: string };
export type ResultadoImportacion = { creados: number; omitidos: number; errores: ErrorDeFila[] };

// Solo Administrador. CSV en UTF-8 con columnas nombre,apellido,email (máximo 200 alumnos sin contar los títulos, 1 MiB),
// enviado como multipart/form-data en el campo «archivo». Las filas con error no frenan a las demás; un email que ya
// existe se omite. Sin `contrasenaInicial`, cada alumno nuevo recibe por email la invitación para definir su contraseña;
// con ella (opcional, S2-ID-02 #135) los alumnos quedan activos y pueden entrar enseguida.
export function importarAlumnos(archivo: File, contrasenaInicial?: string) {
  const formulario = new FormData();
  formulario.append('archivo', archivo);
  if (contrasenaInicial) formulario.append('contrasenaInicial', contrasenaInicial);
  return pedir<ResultadoImportacion>('/api/v1/identidad/usuarios/alumnos/importacion', {
    method: 'POST',
    body: formulario,
  });
}
