import type { ReactNode } from 'react';
import { act, renderHook } from '@testing-library/react';
import { crearTokenFalso } from '@/mocks/token';
import { guardarToken, guardarTokenRenovacion, obtenerToken } from '@/shared/api/cliente';
import { SesionProvider, useSesion } from './SesionContext';

const docente = {
  id: 'u-docente-a',
  nombre: 'Docente A',
  rol: 'docente',
  institucionId: 'institucion-a',
};

const envoltorio = ({ children }: { children: ReactNode }) => (
  <SesionProvider>{children}</SesionProvider>
);

beforeEach(() => guardarToken(null));

describe('SesionContext', () => {
  it('entrar guarda el token y expone al usuario', () => {
    const { result } = renderHook(() => useSesion(), { wrapper: envoltorio });
    const token = crearTokenFalso(docente);

    act(() => {
      result.current.entrar(token);
    });

    expect(result.current.usuario?.rol).toBe('docente');
    expect(obtenerToken()).toBe(token);
  });

  it('salir limpia la sesión', () => {
    const { result } = renderHook(() => useSesion(), { wrapper: envoltorio });
    act(() => {
      result.current.entrar(crearTokenFalso(docente));
    });

    act(() => result.current.salir());

    expect(result.current.usuario).toBeNull();
    expect(obtenerToken()).toBeNull();
  });

  it('recupera al usuario si el token sigue guardado (recarga de página)', () => {
    guardarToken(crearTokenFalso(docente));
    const { result } = renderHook(() => useSesion(), { wrapper: envoltorio });
    expect(result.current.usuario?.id).toBe('u-docente-a');
  });

  it('descarta un token vencido', () => {
    guardarToken(crearTokenFalso(docente, { exp: 1 }));
    const { result } = renderHook(() => useSesion(), { wrapper: envoltorio });
    expect(result.current.usuario).toBeNull();
    expect(obtenerToken()).toBeNull();
  });

  it('conserva un token vencido si hay refreshToken (se renueva en el primer pedido)', () => {
  guardarToken(crearTokenFalso(docente, { exp: 1 }));
  guardarTokenRenovacion('r1');
  const { result } = renderHook(() => useSesion(), { wrapper: envoltorio });
  expect(result.current.usuario?.rol).toBe('docente');
  });

});
