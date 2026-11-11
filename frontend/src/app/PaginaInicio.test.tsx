import { render, screen } from '@testing-library/react';
import { PaginaInicio } from './PaginaInicio';

describe('PaginaInicio', () => {
  it('muestra el título y la ilustración decorativa', () => {
    render(<PaginaInicio />);
    expect(screen.getByRole('heading', { level: 1, name: /Corregí en minutos/ })).toBeInTheDocument();
    expect(screen.getByTestId('ilustracion-inicio')).toHaveAttribute('aria-hidden', 'true');
  });
});
