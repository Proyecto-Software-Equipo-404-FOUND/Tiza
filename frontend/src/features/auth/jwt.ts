import type { Rol, Usuario } from '@/shared/api/tipos';

type Claims = Record<string, unknown>;

const ROLES: Rol[] = ['alumno', 'docente', 'administrador'];

function decodificar(token: string): Claims {
  const carga = token.split('.')[1];
  if (!carga) throw new Error('El token no es válido.');
  const base64 = carga.replace(/-/g, '+').replace(/_/g, '/');
  const binario = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '='));
  const bytes = Uint8Array.from(binario, (caracter) => caracter.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes)) as Claims;
}

function leer(claims: Claims, ...nombres: string[]) {
  for (const nombre of nombres) {
    const valor = claims[nombre];
    if (typeof valor === 'string' && valor) return valor;
  }
  return undefined;
}

// Los nombres de los claims son provisorios hasta confirmarlos con Identidad
export function usuarioDesdeToken(token: string): Usuario {
  const claims = decodificar(token);
  const id = leer(claims, 'UsuarioId', 'usuarioId', 'sub');
  const institucionId = leer(claims, 'InstitucionId', 'institucionId');
  const rol = leer(
    claims,
    'Rol',
    'rol',
    'role',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
  )?.toLowerCase();

  if (!id || !institucionId || !ROLES.includes(rol as Rol)) {
    throw new Error('El token no trae los datos del usuario.');
  }
  return {
    id,
    institucionId,
    rol: rol as Rol,
    nombre: leer(claims, 'nombre', 'name', 'email') ?? '',
  };
}

export function tokenVencido(token: string): boolean {
  const { exp } = decodificar(token);
  return typeof exp === 'number' && exp * 1000 < Date.now();
}
