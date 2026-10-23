import { render, screen } from '@testing-library/react';
import { useQuery } from '@tanstack/react-query';
import { ApiError } from '@/shared/api/cliente';
import { Proveedores } from './Proveedores';

function ConsultaQueFalla({ error }: { error: Error }) {
  useQuery({
    queryKey: ['prueba'],
    queryFn: () => Promise.reject(error),
  });
  return null;
}

function ConsultaContada({ error, alLlamar }: { error: Error; alLlamar: () => void }) {
  useQuery({
    queryKey: ['prueba-conteo'],
    queryFn: () => {
      alLlamar();
      return Promise.reject(error);
    },
  });
  return null;
}

describe('Proveedores', () => {
  it('avisa con un toast cuando una consulta recibe un 403', async () => {
    render(
      <Proveedores>
        <ConsultaQueFalla error={new ApiError(403, { status: 403 })} />
      </Proveedores>,
    );

    expect(await screen.findByText('No tenés permiso para hacer esto.')).toBeInTheDocument();
  });

  it('no avisa globalmente ante otros errores (404) (los muestra cada pantalla)', async () => {
    render(
      <Proveedores>
        <ConsultaQueFalla error={new ApiError(404, { status: 404 })} />
      </Proveedores>,
    );

    await new Promise((resolver) => setTimeout(resolver, 50));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('no reintenta una consulta que recibe un 409', async () => {
    const alLlamar = vi.fn();
    render(
      <Proveedores>
        <ConsultaContada error={new ApiError(409, { status: 409 })} alLlamar={alLlamar} />
      </Proveedores>,
    );

    await new Promise((resolver) => setTimeout(resolver, 50));
    expect(alLlamar).toHaveBeenCalledTimes(1);
  });
});
