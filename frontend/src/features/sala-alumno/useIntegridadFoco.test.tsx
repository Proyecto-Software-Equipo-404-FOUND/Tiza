import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { crearConexionFalsa } from '@/shared/realtime/conexionFalsa';
import { useIntegridadFoco } from './useIntegridadFoco';

const PREGUNTA = '5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60';

let ahora = 1_000_000;
function cambiarVisibilidad(estado: 'hidden' | 'visible') {
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: estado });
  document.dispatchEvent(new Event('visibilitychange'));
}
const avanzar = (ms: number) => {
  ahora += ms;
};

function preparar(opciones: { activo?: boolean; puedeEnviar?: boolean } = {}) {
  const { conexion } = crearConexionFalsa();
  const invocar = vi.fn(async (..._args: unknown[]) => undefined as unknown);
  conexion.invocar = invocar as typeof conexion.invocar;
  const props = { activo: true, puedeEnviar: true, preguntaId: PREGUNTA as string | null, ...opciones };
  const vista = renderHook((p) => useIntegridadFoco(conexion, p), { initialProps: props });
  return { invocar, vista, props };
}

describe('useIntegridadFoco', () => {
  beforeEach(() => {
    ahora = 1_000_000;
    vi.spyOn(Date, 'now').mockImplementation(() => ahora);
    cambiarVisibilidad('visible');
  });
  afterEach(() => {
    vi.restoreAllMocks();
    cambiarVisibilidad('visible');
  });

  it('al cambiar de pestaña y volver envía cuánto tiempo estuvo afuera', async () => {
    const { invocar } = preparar();
    cambiarVisibilidad('hidden');
    avanzar(4200);
    cambiarVisibilidad('visible');

    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(1));
    expect(invocar).toHaveBeenCalledWith('RegistrarEventoFoco', {
      duracionMs: 4200,
      tipoEvento: 'visibilitychange',
      preguntaId: PREGUNTA,
    });
  });

  it('con blur y focus de la ventana también registra el tramo', async () => {
    const { invocar } = preparar();
    act(() => void window.dispatchEvent(new Event('blur')));
    avanzar(1500);
    act(() => void window.dispatchEvent(new Event('focus')));

    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(1));
    expect(invocar).toHaveBeenCalledWith('RegistrarEventoFoco', expect.objectContaining({ duracionMs: 1500, tipoEvento: 'blur' }));
  });

  it('un cambio de pestaña dispara blur y visibilitychange pero se cuenta una sola vez', async () => {
    const { invocar } = preparar();
    act(() => void window.dispatchEvent(new Event('blur')));
    cambiarVisibilidad('hidden');
    avanzar(3000);
    cambiarVisibilidad('visible');
    act(() => void window.dispatchEvent(new Event('focus')));

    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(1));
    expect(invocar).toHaveBeenCalledWith('RegistrarEventoFoco', expect.objectContaining({ duracionMs: 3000 }));
  });

  it('si la pestaña sigue oculta no registra todavía', () => {
    const { invocar } = preparar();
    cambiarVisibilidad('hidden');
    avanzar(2000);
    act(() => void window.dispatchEvent(new Event('focus')));
    expect(invocar).not.toHaveBeenCalled();
  });

  it('no registra nada cuando no está activo', () => {
    const { invocar } = preparar({ activo: false });
    cambiarVisibilidad('hidden');
    avanzar(2000);
    cambiarVisibilidad('visible');
    expect(invocar).not.toHaveBeenCalled();
  });

  it('si el examen termina mientras el alumno está afuera, ese tramo se descarta', () => {
    const { invocar, vista, props } = preparar();
    cambiarVisibilidad('hidden');
    avanzar(2000);
    vista.rerender({ ...props, activo: false });
    cambiarVisibilidad('visible');
    expect(invocar).not.toHaveBeenCalled();
  });

  it('sin conexión guarda el evento y lo manda cuando vuelve', async () => {
    const { invocar, vista, props } = preparar({ puedeEnviar: false });
    cambiarVisibilidad('hidden');
    avanzar(2500);
    cambiarVisibilidad('visible');
    expect(invocar).not.toHaveBeenCalled();

    vista.rerender({ ...props, puedeEnviar: true });
    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(1));
    expect(invocar).toHaveBeenCalledWith('RegistrarEventoFoco', expect.objectContaining({ duracionMs: 2500 }));
  });

  it('si el envío falla por la red, lo reintenta con el siguiente evento sin perder el primero', async () => {
    const { invocar } = preparar();
    invocar.mockRejectedValueOnce(new Error('Connection lost'));
    cambiarVisibilidad('hidden');
    avanzar(1000);
    cambiarVisibilidad('visible');
    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(1));

    cambiarVisibilidad('hidden');
    avanzar(2000);
    cambiarVisibilidad('visible');
    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(3));
    const duraciones = invocar.mock.calls.map((llamada) => (llamada as unknown[])[1] as { duracionMs: number }).map((e) => e.duracionMs);
    expect(duraciones).toEqual([1000, 1000, 2000]);
  });

  it('un rechazo definitivo del servidor descarta el evento y no lo repite', async () => {
    const { invocar } = preparar();
    invocar.mockRejectedValueOnce(new Error('SALA_FINALIZADA: la sala ya finalizó.'));
    cambiarVisibilidad('hidden');
    avanzar(1000);
    cambiarVisibilidad('visible');
    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(1));

    cambiarVisibilidad('hidden');
    avanzar(2000);
    cambiarVisibilidad('visible');
    await waitFor(() => expect(invocar).toHaveBeenCalledTimes(2));
    expect((invocar.mock.calls[1] as unknown[])[1]).toEqual(expect.objectContaining({ duracionMs: 2000 }));
  });
});
