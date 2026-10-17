import { act, renderHook, waitFor } from '@testing-library/react';
import { crearConexionFalsa } from './conexionFalsa';
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
});
