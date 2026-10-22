import { render, screen } from '@testing-library/react';
import { Boton } from './Boton';

describe('Boton', () => {
  it('se deshabilita mientras carga', () => {
    render(<Boton cargando>Guardar</Boton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('muestra el texto cuando no está cargando', () => {
    render(<Boton>Guardar</Boton>);
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeEnabled();
  });

  it('respeta disabled', () => {
    render(<Boton disabled>Guardar</Boton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });
});
