import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useSesion } from '@/features/auth/SesionContext';
import { obtenerMetricas } from '../api/metricas';
import { useMetricas } from './useMetricas';

vi.mock('@/features/auth/SesionContext', () => ({ useSesion: vi.fn() }));
vi.mock('../api/metricas', () => ({ obtenerMetricas: vi.fn() }));

const sesionDe = (institucionId: string | null) =>
  vi.mocked(useSesion).mockReturnValue({
    usuario: institucionId ? { id: 'd1', nombre: 'Docente', rol: 'docente', institucionId } : null,
  } as unknown as ReturnType<typeof useSesion>);

function envoltorio() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => <QueryClientProvider client={cliente}>{children}</QueryClientProvider>;
}

describe('useMetricas', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(obtenerMetricas).mockResolvedValue({
      evaluacionId: 'e1',
      cantidadEntregas: 0,
      cantidadAprobadas: 0,
      puntajeMaximo: 10,
      preguntas: [],
      alumnos: [],
    });
  });

  it('sin sesión no pide nada', () => {
    sesionDe(null);
    const { result } = renderHook(() => useMetricas('e1'), { wrapper: envoltorio() });
    expect(result.current.fetchStatus).toBe('idle');
    expect(obtenerMetricas).not.toHaveBeenCalled();
  });

  it('sin id de evaluación no pide nada', () => {
    sesionDe('i1');
    const { result } = renderHook(() => useMetricas(''), { wrapper: envoltorio() });
    expect(result.current.fetchStatus).toBe('idle');
    expect(obtenerMetricas).not.toHaveBeenCalled();
  });

  it('con sesión pide las métricas de esa evaluación', async () => {
    sesionDe('i1');
    const { result } = renderHook(() => useMetricas('e1'), { wrapper: envoltorio() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(obtenerMetricas).toHaveBeenCalledWith('e1');
  });

  it('al cambiar de institución no reutiliza los datos de la anterior', async () => {
    sesionDe('i1');
    const { result, rerender } = renderHook(() => useMetricas('e1'), { wrapper: envoltorio() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    sesionDe('i2');
    rerender();
    await waitFor(() => expect(obtenerMetricas).toHaveBeenCalledTimes(2));
  });
});
