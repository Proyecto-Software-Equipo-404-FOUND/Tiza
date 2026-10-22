import { tokenVencido, usuarioDesdeToken } from './jwt';

const crear = (claims: Record<string, unknown>) => `cabecera.${btoa(JSON.stringify(claims))}.firma`;

describe('usuarioDesdeToken', () => {
  it('lee los claims de Identidad', () => {
    const token = crear({ UsuarioId: 'u1', InstitucionId: 'i1', Rol: 'Docente' });
    expect(usuarioDesdeToken(token)).toEqual({
      id: 'u1',
      institucionId: 'i1',
      rol: 'docente',
      nombre: '',
    });
  });

  it('acepta los nombres de claim en minúscula', () => {
    const token = crear({ sub: 'u2', institucionId: 'i1', rol: 'alumno', name: 'Ana' });
    expect(usuarioDesdeToken(token)).toMatchObject({ id: 'u2', rol: 'alumno', nombre: 'Ana' });
  });

  it('falla si falta un dato o si no es un token', () => {
    expect(() => usuarioDesdeToken(crear({ UsuarioId: 'u1' }))).toThrow();
    expect(() => usuarioDesdeToken('abc')).toThrow();
  });
});

describe('tokenVencido', () => {
  it('detecta un token vencido', () => {
    expect(tokenVencido(crear({ exp: 1 }))).toBe(true);
  });

  it('acepta uno vigente o sin vencimiento', () => {
    expect(tokenVencido(crear({ exp: Math.floor(Date.now() / 1000) + 3600 }))).toBe(false);
    expect(tokenVencido(crear({}))).toBe(false);
  });
});
