import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { useGoogleDrivePicker } from './useGoogleDrivePicker';

describe('useGoogleDrivePicker', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
  });

  it('indica que no está configurado si faltan variables de entorno', () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    vi.stubEnv('VITE_GOOGLE_API_KEY', '');
    vi.stubEnv('VITE_GOOGLE_APP_ID', '');

    const { result } = renderHook(() => useGoogleDrivePicker());

    expect(result.current.isGoogleDriveConfigurado).toBe(false);
    expect(result.current.isDescargando).toBe(false);
  });

  it('indica que está configurado si las tres variables de entorno están presentes', () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'test-client-id');
    vi.stubEnv('VITE_GOOGLE_API_KEY', 'test-api-key');
    vi.stubEnv('VITE_GOOGLE_APP_ID', '1234567890');

    const { result } = renderHook(() => useGoogleDrivePicker());

    expect(result.current.isGoogleDriveConfigurado).toBe(true);
  });

  it('si no está configurado, abrirPicker notifica el error mediante onError', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', '');
    vi.stubEnv('VITE_GOOGLE_API_KEY', '');
    vi.stubEnv('VITE_GOOGLE_APP_ID', '');

    const { result } = renderHook(() => useGoogleDrivePicker());
    const onArchivoSeleccionado = vi.fn();
    const onError = vi.fn();

    await act(async () => {
      await result.current.abrirPicker({ onArchivoSeleccionado, onError });
    });

    expect(onError).toHaveBeenCalledWith(
      'Google Drive no está configurado en las variables de entorno.',
    );
    expect(onArchivoSeleccionado).not.toHaveBeenCalled();
  });
});
