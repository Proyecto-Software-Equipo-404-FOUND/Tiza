import { useState } from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { Modal } from './Modal';

function Ejemplo({ cerrarAlClicAfuera = true, cerrable = true }: { cerrarAlClicAfuera?: boolean; cerrable?: boolean }) {
  const [abierto, setAbierto] = useState(false);
  return (
    <>
      <button onClick={() => setAbierto(true)}>Abrir</button>
      <Modal
        abierto={abierto}
        titulo='Editar curso'
        subtitulo='Estás editando «Programación I».'
        onCerrar={() => setAbierto(false)}
        cerrarAlClicAfuera={cerrarAlClicAfuera}
        cerrable={cerrable}
      >
        <label>
          Nombre
          <input defaultValue='Programación I' />
        </label>
        <button onClick={() => setAbierto(false)}>Guardar</button>
      </Modal>
    </>
  );
}

const sinVentana = () => waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

describe('Modal', () => {
  it('se ve solo al abrir, con título, subtítulo y contenido', async () => {
    render(<Ejemplo />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    expect(screen.getByRole('dialog', { name: 'Editar curso' })).toBeInTheDocument();
    expect(screen.getByText('Estás editando «Programación I».')).toBeInTheDocument();
    expect(screen.getByLabelText('Nombre')).toBeInTheDocument();
  });

  it('al abrir el foco pasa al primer campo y al cerrar vuelve al botón de origen', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    expect(screen.getByLabelText('Nombre')).toHaveFocus();

    await userEvent.keyboard('{Escape}');
    await sinVentana();
    expect(screen.getByRole('button', { name: 'Abrir' })).toHaveFocus();
  });

  it('el clic afuera cierra, salvo que se pida lo contrario', async () => {
    const { unmount } = render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByRole('dialog').parentElement as HTMLElement);
    await sinVentana();
    unmount();

    render(<Ejemplo cerrarAlClicAfuera={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByRole('dialog').parentElement as HTMLElement);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('si no es cerrable, Escape no la cierra', async () => {
    render(<Ejemplo cerrable={false} />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.keyboard('{Escape}');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('el foco no se escapa de la ventana con Tab', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.tab();
    expect(screen.getByRole('button', { name: 'Guardar' })).toHaveFocus();
    await userEvent.tab();
    expect(screen.getByLabelText('Nombre')).toHaveFocus();
    await userEvent.tab({ shift: true });
    expect(screen.getByRole('button', { name: 'Guardar' })).toHaveFocus();
  });

  it('un botón dentro de la ventana puede cerrarla', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await sinVentana();
  });
});
