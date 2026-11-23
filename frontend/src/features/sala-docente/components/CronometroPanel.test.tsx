import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CronometroPanel } from './CronometroPanel';

describe('CronometroPanel', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('con una duración fija muestra el tiempo completo y no cuenta', () => {
    render(<CronometroPanel etiqueta='Duración' limiteUtc={null} desfaseMs={0} fijoSegundos={1800} />);
    expect(screen.getByRole('timer', { name: 'Duración: 30:00' })).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(5000);
    });
    expect(screen.getByRole('timer', { name: 'Duración: 30:00' })).toBeInTheDocument();
  });

  it('con una fecha límite cuenta hacia atrás', () => {
    const limite = new Date(Date.now() + 90_000).toISOString();
    render(<CronometroPanel etiqueta='Sala' limiteUtc={limite} desfaseMs={0} />);
    expect(screen.getByRole('timer', { name: 'Sala: 01:30' })).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(10_000);
    });
    expect(screen.getByRole('timer', { name: 'Sala: 01:20' })).toBeInTheDocument();
  });

  it('sin fecha límite ni duración muestra «sin fecha»', () => {
    render(<CronometroPanel etiqueta='Sala' limiteUtc={null} desfaseMs={0} />);
    expect(screen.getByRole('timer', { name: 'Sala: sin fecha' })).toBeInTheDocument();
  });
});