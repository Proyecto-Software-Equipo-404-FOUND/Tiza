import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { crearTokenFalso } from '@/mocks/token';
import { ApiError, guardarToken } from '@/shared/api/cliente';
import { AvisosProvider } from '@/shared/components/Aviso';
import * as auth from '@/features/auth/api/auth';
import { SesionProvider } from '@/features/auth/SesionContext';
import { PaginaMiCuenta, validarCambio } from './PaginaMiCuenta';

vi.mock('@/features/auth/api/auth');

const alumno = { id: 'u-alumno1-a', nombre: 'Alumno 1 A', rol: 'alumno', institucionId: 'institucion-a' };

function mostrarPagina() {
  return render(
    <AvisosProvider>
      <QueryClientProvider client={new QueryClient()}>
        <SesionProvider>
          <MemoryRouter initialEntries={['/mi-cuenta']}>
            <Routes>
              <Route path='/mi-cuenta' element={<PaginaMiCuenta />} />
              <Route path='/login' element={<p>Pantalla de login</p>} />
            </Routes>
          </MemoryRouter>
        </SesionProvider>
      </QueryClientProvider>
    </AvisosProvider>,
  );
}

async function completar(actual: string, nueva: string, confirmacion: string) {
  if (actual) await userEvent.type(screen.getByLabelText('Contraseña actual'), actual);
  if (nueva) await userEvent.type(screen.getByLabelText('Contraseña nueva'), nueva);
  if (confirmacion) await userEvent.type(screen.getByLabelText('Repetí la contraseña nueva'), confirmacion);
  await userEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));
}

beforeEach(() => {
  vi.resetAllMocks();
  guardarToken(crearTokenFalso(alumno));
});

describe('validarCambio', () => {
  it('acepta una contraseña nueva válida y confirmada', () => {
    expect(validarCambio('Clave1234', 'OtraClave99', 'OtraClave99')).toEqual({});
  });

  it('rechaza una nueva corta, igual a la actual o sin confirmar', () => {
    expect(validarCambio('Clave1234', 'corta', 'corta').nueva).toMatch(/al menos 8/);
    expect(validarCambio('Clave1234', 'Clave1234', 'Clave1234').nueva).toMatch(/distinta/);
    expect(validarCambio('Clave1234', 'OtraClave99', 'OtraClave98').confirmacion).toMatch(/no coinciden/);
  });
});

describe('PaginaMiCuenta', () => {
  it('no envía si la confirmación no coincide', async () => {
    mostrarPagina();
    await completar('Clave1234', 'OtraClave99', 'OtraClave98');

    expect(await screen.findByText('Las contraseñas no coinciden')).toBeInTheDocument();
    expect(auth.cambiarContrasena).not.toHaveBeenCalled();
  });

  it('al cambiarla cierra la sesión y lleva al login', async () => {
    vi.mocked(auth.cambiarContrasena).mockResolvedValue(undefined);
    vi.mocked(auth.cerrarSesionEnServidor).mockResolvedValue(undefined);
    mostrarPagina();
    await completar('Clave1234', 'OtraClave99', 'OtraClave99');

    expect(await screen.findByText('Pantalla de login')).toBeInTheDocument();
    expect(auth.cambiarContrasena).toHaveBeenCalledWith('Clave1234', 'OtraClave99');
    expect(screen.getByText('Cambiaste tu contraseña. Ingresá de nuevo con la nueva.')).toBeInTheDocument();
  });

  it('muestra el error si la contraseña actual es incorrecta', async () => {
    vi.mocked(auth.cambiarContrasena).mockRejectedValue(
      new ApiError(400, { type: 'CONTRASENA_ACTUAL_INCORRECTA', title: 'x', status: 400 }),
    );
    mostrarPagina();
    await completar('equivocada', 'OtraClave99', 'OtraClave99');

    expect(await screen.findByText('La contraseña actual no es correcta.')).toBeInTheDocument();
    expect(screen.queryByText('Pantalla de login')).not.toBeInTheDocument();
  });
});
