import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RouterProvider, createMemoryRouter } from 'react-router-dom';
import { SesionProvider } from '@/features/auth/SesionContext';
import { crearTokenFalso } from '@/mocks/token';
import { guardarToken } from '@/shared/api/cliente';
import type { Rol } from '@/shared/api/tipos';
import { Proveedores } from './Proveedores';
import { rutas } from './rutas';

vi.mock('@/features/cursos/api/cursos', () => ({
  listarCursos: vi.fn().mockResolvedValue([]),
  crearCurso: vi.fn(),
}));

vi.mock('@/features/cursos/api/cohortes', () => ({
  listarCohortes: vi.fn().mockResolvedValue([]),
  crearCohorte: vi.fn(),
}));

const iniciarComo = (rol: Rol) =>
  guardarToken(
    crearTokenFalso({ id: `u-${rol}`, nombre: `Usuario ${rol}`, rol, institucionId: 'institucion-a' }),
  );

function mostrar(ruta: string) {
  const router = createMemoryRouter(rutas, { initialEntries: [ruta] });
  return render(
    <Proveedores>
      <SesionProvider>
        <RouterProvider router={router} />
      </SesionProvider>
    </Proveedores>,
  );
}

beforeEach(() => guardarToken(null));

describe('rutas de la aplicación', () => {
  it('sin sesión, una ruta protegida lleva al login', () => {
    mostrar('/cursos');
    expect(screen.getByRole('heading', { name: 'Ingresar a Tiza' })).toBeInTheDocument();
  });

  it('un alumno que entra a /cursos ve el aviso de falta de permiso', () => {
    iniciarComo('alumno');
    mostrar('/cursos');
    expect(screen.getByText('No tenés permiso para ver esta página.')).toBeInTheDocument();
  });

  it('un docente entra a /cursos y ve el enlace de navegación', () => {
    iniciarComo('docente');
    mostrar('/cursos');
    expect(screen.getByRole('heading', { name: 'Cursos' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Cursos' })).toBeInTheDocument();
  });

  it('un docente entra a las cohortes de un curso', () => {
    iniciarComo('docente');
    mostrar('/cursos/c-1/cohortes');
    expect(screen.getByRole('heading', { name: 'Cohortes' })).toBeInTheDocument();
  });

  it('un alumno no entra a las cohortes de un curso', () => {
    iniciarComo('alumno');
    mostrar('/cursos/c-1/cohortes');
    expect(screen.getByText('No tenés permiso para ver esta página.')).toBeInTheDocument();
  });

  it('el alumno no ve el enlace a Cursos', () => {
    iniciarComo('alumno');
    mostrar('/');
    expect(screen.queryByRole('link', { name: 'Cursos' })).not.toBeInTheDocument();
  });

  it('quien ya tiene sesión no ve el login', () => {
    iniciarComo('docente');
    mostrar('/login');
    expect(screen.getByRole('heading', { name: 'Cursos' })).toBeInTheDocument();
  });

  it('una ruta inexistente muestra «no encontrada»', () => {
    mostrar('/esto-no-existe');
    expect(screen.getByText('No se encontró lo que buscás.')).toBeInTheDocument();
  });

  it('salir cierra la sesión y lleva al login', async () => {
    iniciarComo('docente');
    mostrar('/cursos');

    await userEvent.click(screen.getByRole('button', { name: 'Salir' }));

    expect(screen.getByRole('heading', { name: 'Ingresar a Tiza' })).toBeInTheDocument();
  });
});
