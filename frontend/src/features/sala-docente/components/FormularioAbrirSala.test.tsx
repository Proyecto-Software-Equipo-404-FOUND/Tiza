import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { FormularioAbrirSala } from './FormularioAbrirSala';

describe('FormularioAbrirSala', () => {
  it('abre la sala con la capacidad por defecto', async () => {
    const onAbrir = vi.fn();
    render(<FormularioAbrirSala cargando={false} onAbrir={onAbrir} />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir sala' }));
    expect(onAbrir).toHaveBeenCalledWith(40);
  });

  it('no envía una capacidad fuera de rango y muestra el error', async () => {
    const onAbrir = vi.fn();
    render(<FormularioAbrirSala cargando={false} onAbrir={onAbrir} />);
    const campo = screen.getByLabelText(/Capacidad máxima/);
    await userEvent.clear(campo);
    await userEvent.type(campo, '101');
    await userEvent.click(screen.getByRole('button', { name: 'Abrir sala' }));
    expect(screen.getByRole('alert')).toHaveTextContent('entre 1 y 100');
    expect(onAbrir).not.toHaveBeenCalled();
  });

  it('pide un valor si el campo queda vacío', async () => {
    const onAbrir = vi.fn();
    render(<FormularioAbrirSala cargando={false} onAbrir={onAbrir} />);
    await userEvent.clear(screen.getByLabelText(/Capacidad máxima/));
    await userEvent.click(screen.getByRole('button', { name: 'Abrir sala' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Ingresá la capacidad');
    expect(onAbrir).not.toHaveBeenCalled();
  });

  it('muestra el error del servidor y deshabilita el botón mientras carga', () => {
    render(<FormularioAbrirSala cargando error='No se pudo abrir la sala. Intentá de nuevo.' onAbrir={vi.fn()} />);
    expect(screen.getByText('No se pudo abrir la sala. Intentá de nuevo.')).toBeInTheDocument();
    expect(screen.getByRole('button')).toBeDisabled();
  });
});