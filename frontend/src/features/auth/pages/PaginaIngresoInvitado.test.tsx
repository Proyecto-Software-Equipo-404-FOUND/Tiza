import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { crearTokenFalso } from '@/mocks/token';
import { ApiError, guardarToken, obtenerToken } from '@/shared/api/cliente';
import * as invitadoApi from '../api/invitado';
import { SesionProvider } from '../SesionContext';
import { PaginaIngresoInvitado } from './PaginaIngresoInvitado';

vi.mock('../api/invitado');

const invitado = {
  id: 'inv-1',
  nombre: 'Lucía Fernández',
  rol: 'invitado',
  institucionId: 'institucion-a',
};

// Lee el PIN que la pantalla de ingreso le pasa a la sala por el state del router
function PantallaSala() {
  const { state } = useLocation();
  const pin = (state as { pin?: string } | null)?.pin;
  return <p>{`Pantalla de sala con PIN ${pin}`}</p>;
}

function mostrarPagina(ruta = '/invitado') {
  return render(
    <SesionProvider>
      <MemoryRouter initialEntries={[ruta]}>
        <Routes>
          <Route path='/invitado' element={<PaginaIngresoInvitado />} />
          <Route path='/login' element={<p>Pantalla de login</p>} />
          <Route path='/sala' element={<PantallaSala />} />
        </Routes>
      </MemoryRouter>
    </SesionProvider>,
  );
}

async function completar(pin: string, nombre: string) {
  await userEvent.type(screen.getByLabelText('PIN de la sala'), pin);
  await userEvent.type(screen.getByLabelText('Nombre y apellido'), nombre);
}

const entrar = () => userEvent.click(screen.getByRole('button', { name: 'Entrar a la sala' }));

beforeEach(() => {
  vi.resetAllMocks();
  guardarToken(null);
});

describe('PaginaIngresoInvitado', () => {
  it('no envía con los campos vacíos', async () => {
    mostrarPagina();
    await entrar();

    expect(await screen.findByText('Ingresá el PIN de 6 dígitos')).toBeInTheDocument();
    expect(screen.getByText('Ingresá tu nombre y apellido (al menos 2 caracteres)')).toBeInTheDocument();
    expect(invitadoApi.ingresarComoInvitado).not.toHaveBeenCalled();
  });

  it.each(['12345', '012345'])('rechaza el PIN %s (tiene que ser de 6 dígitos y no empezar con 0)', async (pin) => {
    mostrarPagina();
    await completar(pin, 'Lucía Fernández');
    await entrar();

    expect(await screen.findByText('Ingresá el PIN de 6 dígitos')).toBeInTheDocument();
    expect(invitadoApi.ingresarComoInvitado).not.toHaveBeenCalled();
  });

  it('rechaza un nombre de una sola letra', async () => {
    mostrarPagina();
    await completar('482913', 'a');
    await entrar();

    expect(await screen.findByText('Ingresá tu nombre y apellido (al menos 2 caracteres)')).toBeInTheDocument();
    expect(invitadoApi.ingresarComoInvitado).not.toHaveBeenCalled();
  });

  it('en el PIN solo deja escribir dígitos', async () => {
    mostrarPagina();
    await userEvent.type(screen.getByLabelText('PIN de la sala'), '12a3b45');

    expect(screen.getByLabelText('PIN de la sala')).toHaveValue('12345');
  });

  it('con PIN y nombre válidos entra, guarda la sesión y lleva a la sala con el PIN', async () => {
    const token = crearTokenFalso(invitado, { salaId: 'sala-1' });
    vi.mocked(invitadoApi.ingresarComoInvitado).mockResolvedValue({
      accessToken: token,
      expiraEnUtc: '2026-11-21T22:00:00Z',
      salaId: 'sala-1',
      nombre: 'Lucía Fernández',
    });
    mostrarPagina();
    await completar('482913', '  Lucía Fernández  ');
    await entrar();

    expect(await screen.findByText('Pantalla de sala con PIN 482913')).toBeInTheDocument();
    // El PIN viaja como número y el nombre sin los espacios de los costados
    expect(invitadoApi.ingresarComoInvitado).toHaveBeenCalledWith(482913, 'Lucía Fernández');
    expect(obtenerToken()).toBe(token);
  });

  it.each([
    [400, 'NOMBRE_INVALIDO', 'El nombre tiene que tener entre 2 y 60 caracteres.'],
    [403, 'SALA_SIN_INVITADOS', 'Esta sala no admite invitados. Pedile al docente que te habilite.'],
    [404, 'SALA_NO_ENCONTRADA', 'No hay una sala activa con ese PIN. Revisá el número e intentá de nuevo.'],
    [409, 'SALA_FINALIZADA', 'La sala ya finalizó.'],
    [503, 'SALA_NO_DISPONIBLE', 'No pudimos verificar la sala. Probá de nuevo.'],
  ])('ante un %s (%s) muestra el mensaje del ingreso y no entra', async (estado, codigo, texto) => {
    vi.mocked(invitadoApi.ingresarComoInvitado).mockRejectedValue(new ApiError(estado, { status: estado, type: codigo }));
    mostrarPagina();
    await completar('482913', 'Lucía Fernández');
    await entrar();

    expect(await screen.findByText(texto)).toBeInTheDocument();
    expect(screen.queryByText(/Pantalla de sala/)).not.toBeInTheDocument();
  });

  it('deshabilita el botón mientras espera la respuesta', async () => {
    vi.mocked(invitadoApi.ingresarComoInvitado).mockReturnValue(new Promise(() => {}));
    mostrarPagina();
    await completar('482913', 'Lucía Fernández');
    await entrar();

    expect(await screen.findByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });

  it('si el acceso venció (?vencido=1) avisa que tiene que volver a ingresar', () => {
    mostrarPagina('/invitado?vencido=1');

    expect(screen.getByText('Tu acceso como invitado venció. Volvé a ingresar con el PIN y tu nombre.')).toBeInTheDocument();
  });

  it('sin ese parámetro no muestra el aviso de acceso vencido', () => {
    mostrarPagina();

    expect(screen.queryByText(/Tu acceso como invitado venció/)).not.toBeInTheDocument();
  });

  it('ofrece volver al login a quien tiene cuenta', () => {
    mostrarPagina();

    expect(screen.getByRole('link', { name: 'Tengo una cuenta de Tiza' })).toHaveAttribute('href', '/login');
  });
});
