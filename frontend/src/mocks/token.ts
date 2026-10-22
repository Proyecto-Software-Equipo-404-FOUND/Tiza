type UsuarioFalso = { id: string; nombre: string; rol: string; institucionId: string };

const base64url = (texto: string) =>
  btoa(texto).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

// JWT falso (sin firma real) con los mismos claims que va a traer el de Identidad
export function crearTokenFalso(usuario: UsuarioFalso, opciones: { exp?: number } = {}) {
  const cabecera = base64url(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const carga = base64url(
    JSON.stringify({
      UsuarioId: usuario.id,
      InstitucionId: usuario.institucionId,
      Rol: usuario.rol,
      nombre: usuario.nombre,
      ...(opciones.exp !== undefined ? { exp: opciones.exp } : {}),
    }),
  );
  return `${cabecera}.${carga}.firma-falsa`;
}
