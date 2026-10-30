import type { ComponentProps } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { AccionesFila } from './AccionesFila';

function mostrar(extra: Partial<ComponentProps<typeof AccionesFila>> = {}) {
  const onEditar = vi.fn();
  const onEliminar = vi.fn();
  render(<AccionesFila nombre='Programación I' onEditar={onEditar} onEliminar={onEliminar} {...extra} />);
  return { onEditar, onEliminar };
}

describe('AccionesFila', () => {
  it('llama a onEditar al tocar Editar', async () => {
    const { onEditar } = mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Editar Programación I' }));
    expect(onEditar).toHaveBeenCalledTimes(1);
  });

  it('pide confirmación antes de eliminar y no elimina todavía', async () => {
    const { onEliminar } = mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Programación I' }));

    expect(screen.getByText('¿Eliminar?')).toBeInTheDocument();
    expect(onEliminar).not.toHaveBeenCalled();
  });

  it('elimina solo cuando se confirma', async () => {
    const { onEliminar } = mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Programación I' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));

    expect(onEliminar).toHaveBeenCalledTimes(1);
  });

  it('cancelar vuelve a los botones sin eliminar', async () => {
    const { onEliminar } = mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar Programación I' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));

    expect(onEliminar).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Editar Programación I' })).toBeInTheDocument();
  });

  it('deshabilita Eliminar mientras se elimina', () => {
    mostrar({ eliminando: true });
    expect(screen.getByRole('button', { name: 'Eliminar Programación I' })).toBeDisabled();
  });
});
