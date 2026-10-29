import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Cronometro, formatearTiempo } from './Cronometro';

describe('formatearTiempo', () => {
  it('muestra minutos y segundos con dos dígitos', () => {
    expect(formatearTiempo(65_000)).toBe('01:05');
    expect(formatearTiempo(9_000)).toBe('00:09');
    expect(formatearTiempo(0)).toBe('00:00');
  });
});

describe('Cronometro', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-10-28T14:00:00.000Z'));
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('cuenta hacia atrás cada segundo', () => {
    render(<Cronometro limiteUtc='2026-10-28T14:01:05.000Z' desfaseMs={0} />);
    expect(screen.getByRole('timer')).toHaveTextContent('01:05');

    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(screen.getByRole('timer')).toHaveTextContent('01:00');
  });

  it('avisa «Tiempo agotado» cuando llega el límite', () => {
    render(<Cronometro limiteUtc='2026-10-28T14:00:03.000Z' desfaseMs={0} />);
    act(() => {
      vi.advanceTimersByTime(4000);
    });
    expect(screen.getByRole('timer')).toHaveTextContent('Tiempo agotado');
  });

  it('usa el reloj del servidor: si el navegador está atrasado, descuenta la diferencia', () => {
    // El servidor va 30 s adelantado respecto del navegador
    render(<Cronometro limiteUtc='2026-10-28T14:01:00.000Z' desfaseMs={30_000} />);
    expect(screen.getByRole('timer')).toHaveTextContent('00:30');
  });
});
