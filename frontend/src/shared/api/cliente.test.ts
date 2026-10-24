import {
  ApiError,
  guardarToken,
  guardarTokenRenovacion,
  obtenerToken,
  obtenerTokenRenovacion,
  pedir,
} from './cliente';

const asignar = vi.fn();

function responder(estado: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: estado }), { status: estado })),
  );
}

const json = (cuerpo: unknown, estado = 200) =>
  new Response(JSON.stringify(cuerpo), { status: estado });

beforeEach(() => {
  asignar.mockReset();
  vi.stubGlobal('location', { assign: asignar });
});

afterEach(() => {
  vi.unstubAllGlobals();
  guardarToken(null);
});

describe('pedir ante un 401', () => {
  it('limpia el token y lleva al login si había sesión', async () => {
    guardarToken('abc');
    responder(401);

    await expect(pedir('/api/v1/cursos')).rejects.toBeInstanceOf(ApiError);

    expect(asignar).toHaveBeenCalledWith('/login');
    expect(obtenerToken()).toBeNull();
  });

  it('no redirige si no había token (credenciales incorrectas)', async () => {
    guardarToken(null);
    responder(401);

    await expect(pedir('/api/v1/sesiones')).rejects.toBeInstanceOf(ApiError);

    expect(asignar).not.toHaveBeenCalled();
  });
});

describe('renovación del token', () => {
  it('renueva con el refreshToken y reintenta el pedido', async () => {
    guardarToken('viejo');
    guardarTokenRenovacion('r1');
    const fetchFalso = vi
      .fn()
      .mockResolvedValueOnce(json({}, 401))
      .mockResolvedValueOnce(json({ accessToken: 'nuevo', refreshToken: 'r2' }))
      .mockResolvedValueOnce(json({ ok: true }));
    vi.stubGlobal('fetch', fetchFalso);

    await expect(pedir('/api/v1/cursos')).resolves.toEqual({ ok: true });

    expect(obtenerToken()).toBe('nuevo');
    expect(obtenerTokenRenovacion()).toBe('r2');
    expect(asignar).not.toHaveBeenCalled();
    expect(fetchFalso).toHaveBeenCalledTimes(3);
  });

  it('si la renovación falla, limpia la sesión y lleva al login', async () => {
    guardarToken('viejo');
    guardarTokenRenovacion('r1');
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValueOnce(json({}, 401)).mockResolvedValueOnce(json({}, 401)),
    );

    await expect(pedir('/api/v1/cursos')).rejects.toBeInstanceOf(ApiError);

    expect(asignar).toHaveBeenCalledWith('/login');
    expect(obtenerToken()).toBeNull();
    expect(obtenerTokenRenovacion()).toBeNull();
  });

  it('dos pedidos con 401 comparten una sola renovación', async () => {
    guardarToken('viejo');
    guardarTokenRenovacion('r1');
    let renovaciones = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        if (url.includes('/auth/refresh')) {
          renovaciones += 1;
          return json({ accessToken: 'nuevo' });
        }
        return obtenerToken() === 'nuevo' ? json({ ok: true }) : json({}, 401);
      }),
    );

    await Promise.all([pedir('/api/v1/cursos'), pedir('/api/v1/cursos/x/cohortes')]);

    expect(renovaciones).toBe(1);
  });

  it('un 401 en una ruta de auth no intenta renovar', async () => {
    guardarToken('viejo');
    guardarTokenRenovacion('r1');
    const fetchFalso = vi.fn().mockResolvedValue(json({}, 401));
    vi.stubGlobal('fetch', fetchFalso);

    await expect(pedir('/api/v1/identidad/auth/logout')).rejects.toBeInstanceOf(ApiError);

    expect(fetchFalso).toHaveBeenCalledTimes(1);
  });
});