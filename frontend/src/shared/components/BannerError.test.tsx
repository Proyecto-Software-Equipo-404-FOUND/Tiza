import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BannerError } from './BannerError';

describe('BannerError', () => {
  it('llama a onReintentar al apretar el botón', async () => {
    const onReintentar = vi.fn();
    render(<BannerError onReintentar={onReintentar} />);
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(onReintentar).toHaveBeenCalledTimes(1);
  });

  it('no muestra el botón si no hay onReintentar', () => {
    render(<BannerError mensaje='Error' />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
