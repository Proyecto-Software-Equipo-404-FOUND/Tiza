import { ApiError, guardarToken, obtenerToken, pedir } from './cliente';

const asignar = vi.fn();

function responder(estado: number) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: estado }), { status: estado })),
  );
}

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
