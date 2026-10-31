import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ModalConfirmacion } from './ModalConfirmacion';

function Ejemplo({ onConfirmar = vi.fn(), cargando = false }: { onConfirmar?: () => void; cargando?: boolean }) {
  const [abierto, setAbierto] = useState(false);
  return (
    <>
      <button onClick={() => setAbierto(true)}>Abrir</button>
      <ModalConfirmacion
        abierto={abierto}
        titulo='¿Eliminar curso?'
        mensaje='No se puede deshacer.'
        cargando={cargando}
        onCancelar={() => setAbierto(false)}
        onConfirmar={() => {
          setAbierto(false);
          onConfirmar();
        }}
      />
    </>
  );
}

const sinCartel = () => waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

describe('ModalConfirmacion', () => {
  it('no se ve hasta que se abre y tiene los datos accesibles', async () => {
    render(<Ejemplo />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    const cartel = screen.getByRole('dialog', { name: '¿Eliminar curso?' });
    expect(cartel).toHaveAttribute('aria-modal', 'true');
    expect(cartel).toHaveAccessibleDescription('No se puede deshacer.');
  });

  it('al abrir el foco queda en «Cancelar»', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    expect(screen.getByRole('button', { name: 'Cancelar' })).toHaveFocus();
  });

  it('cancelar cierra el cartel con la animación y devuelve el foco al botón de origen', async () => {
    const onConfirmar = vi.fn();
    render(<Ejemplo onConfirmar={onConfirmar} />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));

    await sinCartel();
    expect(onConfirmar).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Abrir' })).toHaveFocus();
  });

  it('Escape cierra el cartel', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.keyboard('{Escape}');
    await sinCartel();
  });

  it('hacer clic afuera cierra el cartel, pero hacer clic adentro no', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));

    await userEvent.click(screen.getByRole('dialog'));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('dialog').parentElement as HTMLElement);
    await sinCartel();
  });

  it('confirmar llama a onConfirmar una sola vez', async () => {
    const onConfirmar = vi.fn();
    render(<Ejemplo onConfirmar={onConfirmar} />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByRole('button', { name: 'Sí, eliminar' }));

    expect(onConfirmar).toHaveBeenCalledTimes(1);
    await sinCartel();
  });

  it('el foco no se escapa del cartel con Tab', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Sí, eliminar' })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Cancelar' })).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(screen.getByRole('button', { name: 'Sí, eliminar' })).toHaveFocus();
  });

  it('mientras está cargando no se puede cancelar ni cerrar con Escape', async () => {
    function Abierto() {
      return (
        <ModalConfirmacion abierto titulo='T' mensaje='M' cargando onCancelar={onCancelar} onConfirmar={() => undefined} />
      );
    }
    const onCancelar = vi.fn();
    render(<Abierto />);
    expect(screen.getByRole('button', { name: 'Cancelar' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cargando…' })).toBeDisabled();

    await userEvent.keyboard('{Escape}');
    expect(onCancelar).not.toHaveBeenCalled();
  });
});
