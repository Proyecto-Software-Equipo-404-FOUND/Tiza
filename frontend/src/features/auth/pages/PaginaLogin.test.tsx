import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { crearTokenFalso } from '@/mocks/token';
import { ApiError, guardarToken } from '@/shared/api/cliente';
import * as auth from '../api/auth';
import { SesionProvider } from '../SesionContext';
import { PaginaLogin } from './PaginaLogin';

vi.mock('../api/auth');

const docente = {
  id: 'u-docente-a',
  nombre: 'Docente A',
  rol: 'docente',
  institucionId: 'institucion-a',
};
const alumno = {
  id: 'u-alumno1-a',
  nombre: 'Alumno 1 A',
  rol: 'alumno',
  institucionId: 'institucion-a',
};

function mostrarPagina() {
  return render(
    <SesionProvider>
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path='/login' element={<PaginaLogin />} />
          <Route path='/cursos' element={<p>Pantalla de cursos</p>} />
          <Route path='/' element={<p>Inicio del alumno</p>} />
        </Routes>
      </MemoryRouter>
    </SesionProvider>,
  );
}

async function completar(correo: string, contrasena: string) {
  await userEvent.type(screen.getByLabelText('Correo'), correo);
  await userEvent.type(screen.getByLabelText('Contraseña'), contrasena);
}

const ingresar = () => userEvent.click(screen.getByRole('button', { name: 'Ingresar' }));

beforeEach(() => {
  vi.resetAllMocks();
  guardarToken(null);
});

describe('PaginaLogin', () => {
  it('no envía con los campos vacíos', async () => {
    mostrarPagina();
    await ingresar();

    expect(await screen.findByText('Ingresá tu correo')).toBeInTheDocument();
    expect(screen.getByText('Ingresá tu contraseña')).toBeInTheDocument();
    expect(auth.iniciarSesion).not.toHaveBeenCalled();
  });

  it('valida el formato del correo', async () => {
    mostrarPagina();
    await completar('abc', 'clave');
    await ingresar();

    expect(await screen.findByText('Ingresá un correo válido')).toBeInTheDocument();
    expect(auth.iniciarSesion).not.toHaveBeenCalled();
  });

  it('muestra un mensaje genérico ante credenciales incorrectas (401)', async () => {
    vi.mocked(auth.iniciarSesion).mockRejectedValue(new ApiError(401, { status: 401 }));
    mostrarPagina();
    await completar('docente@a.test', 'mala');
    await ingresar();

    expect(await screen.findByText('Correo o contraseña incorrectos')).toBeInTheDocument();
  });

  it('muestra el mensaje general ante otro error', async () => {
    vi.mocked(auth.iniciarSesion).mockRejectedValue(new ApiError(500, null));
    mostrarPagina();
    await completar('docente@a.test', 'clave');
    await ingresar();

    expect(await screen.findByText('Algo salió mal. Probá de nuevo.')).toBeInTheDocument();
  });

  it('deshabilita el botón mientras espera la respuesta', async () => {
    vi.mocked(auth.iniciarSesion).mockReturnValue(new Promise(() => {}));
    mostrarPagina();
    await completar('docente@a.test', 'clave');
    await ingresar();

    expect(await screen.findByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });

  it('lleva al docente a /cursos y permite enviar con Enter', async () => {
    vi.mocked(auth.iniciarSesion).mockResolvedValue({ accessToken: crearTokenFalso(docente) });
    mostrarPagina();
    await completar('docente@a.test', 'clave');
    await userEvent.type(screen.getByLabelText('Contraseña'), '{Enter}');

    expect(await screen.findByText('Pantalla de cursos')).toBeInTheDocument();
    expect(auth.iniciarSesion).toHaveBeenCalledWith('docente@a.test', 'clave');
  });

  it('lleva al alumno a la pantalla de inicio', async () => {
    vi.mocked(auth.iniciarSesion).mockResolvedValue({ accessToken: crearTokenFalso(alumno) });
    mostrarPagina();
    await completar('alumno1@a.test', 'clave');
    await ingresar();

    expect(await screen.findByText('Inicio del alumno')).toBeInTheDocument();
  });
});
