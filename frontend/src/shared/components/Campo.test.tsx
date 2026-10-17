import { render, screen } from '@testing-library/react';
import { Campo } from './Campo';

describe('Campo', () => {
  it('asocia la etiqueta con el campo', () => {
    render(<Campo etiqueta='Correo' />);
    expect(screen.getByLabelText('Correo')).toBeInTheDocument();
  });

  it('muestra el mensaje de error debajo del campo', () => {
    render(<Campo etiqueta='Correo' error='Ingresá un correo válido' />);
    expect(screen.getByRole('alert')).toHaveTextContent('Ingresá un correo válido');
    expect(screen.getByLabelText('Correo')).toHaveAttribute('aria-invalid', 'true');
  });

  it('puede estar deshabilitado', () => {
    render(<Campo etiqueta='Correo' disabled />);
    expect(screen.getByLabelText('Correo')).toBeDisabled();
  });
});
