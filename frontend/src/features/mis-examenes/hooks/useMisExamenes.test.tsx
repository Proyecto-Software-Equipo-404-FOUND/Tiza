import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useSesion } from '@/features/auth/SesionContext';
import { listarMisExamenes } from '../api/misExamenes';
import { useMisExamenes } from './useMisExamenes';

vi.mock('@/features/auth/SesionContext', () => ({ useSesion: vi.fn() }));
vi.mock('../api/misExamenes', () => ({ listarMisExamenes: vi.fn() }));

const sesionDe = (id: string | null) =>
  vi.mocked(useSesion).mockReturnValue({
    usuario: id ? { id, nombre: 'Alumno', rol: 'alumno', institucionId: 'i1' } : null,
  } as unknown as ReturnType<typeof useSesion>);

function envoltorio() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => <QueryClientProvider client={cliente}>{children}</QueryClientProvider>;
}

describe('useMisExamenes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(listarMisExamenes).mockResolvedValue([]);
  });

  it('sin sesión no pide nada', () => {
    sesionDe(null);
    const { result } = renderHook(() => useMisExamenes(), { wrapper: envoltorio() });
    expect(result.current.fetchStatus).toBe('idle');
    expect(listarMisExamenes).not.toHaveBeenCalled();
  });

  it('con sesión pide los exámenes del alumno', async () => {
    sesionDe('a1');
    const { result } = renderHook(() => useMisExamenes(), { wrapper: envoltorio() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(listarMisExamenes).toHaveBeenCalledTimes(1);
  });

  it('al cambiar de alumno no reutiliza los datos del anterior', async () => {
    sesionDe('a1');
    const { result, rerender } = renderHook(() => useMisExamenes(), { wrapper: envoltorio() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    sesionDe('a2');
    rerender();
    await waitFor(() => expect(listarMisExamenes).toHaveBeenCalledTimes(2));
  });
});
