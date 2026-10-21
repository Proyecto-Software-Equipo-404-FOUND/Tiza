import { StrictMode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { crearConexionFalsa } from './conexionFalsa';
import type { ConexionSala } from './tipos';
import { useConexionSala } from './useConexionSala';

describe('useConexionSala', () => {
  it('pasa a conectado al iniciar', async () => {
    const { conexion } = crearConexionFalsa();
    const { result } = renderHook(() => useConexionSala(conexion));

    await waitFor(() => expect(result.current.estado).toBe('conectado'));
  });

  it('refleja una reconexión simulada', async () => {
    const { conexion, simularEstado } = crearConexionFalsa();
    const { result } = renderHook(() => useConexionSala(conexion));
    await waitFor(() => expect(result.current.estado).toBe('conectado'));

    act(() => simularEstado('reconectando'));

    expect(result.current.estado).toBe('reconectando');
  });

  it('en StrictMode no vuelve a iniciar hasta que termina de detenerse', async () => {
    const eventos: string[] = [];
    let terminarDetencion: () => void = () => {};
    const { conexion: base } = crearConexionFalsa();
    const conexion: ConexionSala = {
      ...base,
      iniciar: async () => {
        eventos.push('iniciar');
      },
      detener: () =>
        new Promise<void>((resolver) => {
          eventos.push('detener');
          terminarDetencion = resolver;
        }),
    };

    renderHook(() => useConexionSala(conexion), { wrapper: StrictMode });

    await waitFor(() => expect(eventos).toEqual(['iniciar', 'detener']));
    act(() => terminarDetencion());
    await waitFor(() => expect(eventos).toEqual(['iniciar', 'detener', 'iniciar']));
  });
});
