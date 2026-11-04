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

  it('Escape también cierra si se hizo clic en un texto de la ventana', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByText('Estás editando «Programación I».'));
    await userEvent.keyboard('{Escape}');
    await sinVentana();
  });

  it('con el foco afuera, Tab lo devuelve a la ventana', async () => {
    render(<Ejemplo />);
    await userEvent.click(screen.getByRole('button', { name: 'Abrir' }));
    await userEvent.click(screen.getByText('Estás editando «Programación I».'));
    await userEvent.tab();
    expect(screen.getByLabelText('Nombre')).toHaveFocus();
  });

  it('si desaparece el botón de origen, el foco pasa al título de la página', async () => {
    function Lista() {
      const [hay, setHay] = useState(true);
      const [abierto, setAbierto] = useState(false);
      return (
        <main>
          <h1>Cursos</h1>
          {hay && (
            <>
              <button onClick={() => setAbierto(true)}>Editar fila</button>
              <Modal abierto={abierto} titulo='Editar' onCerrar={() => setAbierto(false)}>
                <button
                  onClick={() => {
                    setAbierto(false);
                    setTimeout(() => setHay(false), 0);
                  }}
                >
                  Borrar
                </button>
              </Modal>
            </>
          )}
        </main>
      );
    }
    render(<Lista />);
    await userEvent.click(screen.getByRole('button', { name: 'Editar fila' }));
    await userEvent.click(screen.getByRole('button', { name: 'Borrar' }));
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Editar fila' })).not.toBeInTheDocument());
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Cursos' })).toHaveFocus());
  });
});
