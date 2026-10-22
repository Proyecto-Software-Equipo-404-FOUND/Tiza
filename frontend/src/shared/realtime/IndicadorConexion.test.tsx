import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IndicadorConexion } from './IndicadorConexion';

describe('IndicadorConexion', () => {
  it('muestra el aviso de reconexión', () => {
    render(<IndicadorConexion estado='reconectando' />);
    expect(screen.getByRole('alert')).toHaveTextContent('Se cortó la conexión. Reconectando…');
  });

  it('muestra un botón para reintentar al desconectarse', async () => {
    const onReintentar = vi.fn();
    render(<IndicadorConexion estado='desconectado' onReintentar={onReintentar} />);

    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));

    expect(onReintentar).toHaveBeenCalledTimes(1);
  });

  it('muestra conectado', () => {
    render(<IndicadorConexion estado='conectado' />);
    expect(screen.getByRole('status')).toHaveTextContent('Conectado');
  });
});
