type UsuarioFalso = { id: string; nombre: string; rol: string; institucionId: string };

const base64url = (texto: string) =>
  btoa(texto).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

// El backend arma el rol con mayúscula inicial: Docente, Alumno, Administrador
const capitalizar = (texto: string) => texto.charAt(0).toUpperCase() + texto.slice(1);

// JWT falso (sin firma real) con los mismos claims que genera Identidad: sub, institucion_id y role
export function crearTokenFalso(usuario: UsuarioFalso, opciones: { exp?: number } = {}) {
  const cabecera = base64url(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const carga = base64url(
    JSON.stringify({
      sub: usuario.id,
      institucion_id: usuario.institucionId,
      role: capitalizar(usuario.rol),
      nombre: usuario.nombre,
      ...(opciones.exp !== undefined ? { exp: opciones.exp } : {}),
    }),
  );
  return `${cabecera}.${carga}.firma-falsa`;
}