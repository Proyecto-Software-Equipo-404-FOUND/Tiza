// El rol viaja en el JWT (claim role). Si no se puede leer se trata como sin sesión.
export function rolDelToken(request: Request): string | null {
  const encabezado = request.headers.get('Authorization');
  const carga = encabezado?.replace('Bearer ', '').split('.')[1];
  if (!carga) return null;
  try {
    const json = atob(carga.replace(/-/g, '+').replace(/_/g, '/'));
    return (JSON.parse(json) as { role?: string }).role ?? null;
  } catch {
    return null;
  }
}

type UsuarioFalso = { id: string; nombre: string; rol: string; institucionId: string };

// btoa solo entiende Latin-1: se pasa por UTF-8 para que un nombre con tildes o ñ llegue bien, como en un JWT real
const base64url = (texto: string) => {
  const binario = Array.from(new TextEncoder().encode(texto), (byte) => String.fromCharCode(byte)).join('');
  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
};

// El backend arma el rol con mayúscula inicial: Docente, Alumno, Administrador, Invitado
const capitalizar = (texto: string) => texto.charAt(0).toUpperCase() + texto.slice(1);

// JWT falso (sin firma real) con los mismos claims que genera Identidad: sub, institucion_id y role.
// Con `salaId` (token del Invitado) suma sala_id y name, como dice el contrato identidad-v1.
export function crearTokenFalso(usuario: UsuarioFalso, opciones: { exp?: number; salaId?: string } = {}) {
  const cabecera = base64url(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const carga = base64url(
    JSON.stringify({
      sub: usuario.id,
      institucion_id: usuario.institucionId,
      role: capitalizar(usuario.rol),
      nombre: usuario.nombre,
      ...(opciones.salaId !== undefined ? { sala_id: opciones.salaId, name: usuario.nombre } : {}),
      ...(opciones.exp !== undefined ? { exp: opciones.exp } : {}),
    }),
  );
  return `${cabecera}.${carga}.firma-falsa`;
}