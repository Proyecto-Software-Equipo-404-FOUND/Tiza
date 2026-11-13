import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { PaginaUsuarios } from './PaginaUsuarios';
import * as api from './api/usuarios';

vi.mock('./api/usuarios');
const mostrarAviso = vi.fn();
vi.mock('@/shared/components/Aviso', () => ({ useAviso: () => ({ mostrar: mostrarAviso }) }));

function mostrar() {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>
        <PaginaUsuarios />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

async function completar(email = 'ana@a.test', password = 'Clave1234') {
  await userEvent.type(screen.getByLabelText('Nombre'), 'Ana');
  await userEvent.type(screen.getByLabelText('Apellido'), 'Paz');
  await userEvent.type(screen.getByLabelText('Email'), email);
  await userEvent.type(screen.getByLabelText('Repetir email'), email);
  await userEvent.type(screen.getByLabelText('Contraseña inicial'), password);
  await userEvent.type(screen.getByLabelText('Repetir contraseña'), password);
}

describe('PaginaUsuarios', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(api.listarUsuarios).mockResolvedValue([]);
  });

  it('muestra el estado vacío cuando no hay usuarios', async () => {
    mostrar();
    expect(await screen.findByText('Todavía no hay usuarios.')).toBeInTheDocument();
  });

  it('lista alumnos y docentes con su email y su rol', async () => {
    vi.mocked(api.listarUsuarios).mockImplementation((rol) =>
      Promise.resolve(
        rol === 'Alumno'
          ? [{ id: 'a-1', nombre: 'Ana Paz', email: 'ana@a.test', rol: 'Alumno' }]
          : [{ id: 'd-1', nombre: 'Marta Díaz', email: 'marta@a.test', rol: 'Docente' }],
      ),
    );
    mostrar();
    expect(await screen.findByText('Ana Paz')).toBeInTheDocument();
    expect(screen.getByText('ana@a.test')).toBeInTheDocument();
    expect(screen.getByText('Marta Díaz')).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Docente' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Alumno' })).toBeInTheDocument();
  });

  it('avisa si no se pudo cargar la lista', async () => {
    vi.mocked(api.listarUsuarios).mockRejectedValue(new ApiError(500, null));
    mostrar();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
  });

  it('no envía si faltan datos', async () => {
    mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Crear alumno' }));
    expect(screen.getByText('Ingresá un nombre')).toBeInTheDocument();
    expect(screen.getByText('Ingresá un apellido')).toBeInTheDocument();
    expect(screen.getByText('Ingresá un email')).toBeInTheDocument();
    expect(screen.getByText('Mínimo 8 caracteres')).toBeInTheDocument();
    expect(api.registrarUsuario).not.toHaveBeenCalled();
  });

  it('no envía con un email inválido', async () => {
    mostrar();
    await completar('no-es-un-email');
    await userEvent.click(screen.getByRole('button', { name: 'Crear alumno' }));
    expect(screen.getByText('Ingresá un email válido')).toBeInTheDocument();
    expect(api.registrarUsuario).not.toHaveBeenCalled();
  });

  it('no envía si el email repetido no coincide', async () => {
    mostrar();
    await completar();
    await userEvent.clear(screen.getByLabelText('Repetir email'));
    await userEvent.type(screen.getByLabelText('Repetir email'), 'otra@a.test');
    await userEvent.click(screen.getByRole('button', { name: 'Crear alumno' }));

    expect(screen.getByText('Los emails no coinciden')).toBeInTheDocument();
    expect(api.registrarUsuario).not.toHaveBeenCalled();
  });

  it('no envía si la confirmación no coincide', async () => {
    mostrar();
    await completar();
    await userEvent.clear(screen.getByLabelText('Repetir contraseña'));
    await userEvent.type(screen.getByLabelText('Repetir contraseña'), 'Otra12345');
    await userEvent.click(screen.getByRole('button', { name: 'Crear alumno' }));

    expect(screen.getByText('Las contraseñas no coinciden')).toBeInTheDocument();
    expect(api.registrarUsuario).not.toHaveBeenCalled();
  });

  it('permite mostrar y ocultar las contraseñas', async () => {
    mostrar();
    expect(screen.getByLabelText('Contraseña inicial')).toHaveAttribute('type', 'password');
    expect(screen.getByLabelText('Repetir contraseña')).toHaveAttribute('type', 'password');

    await userEvent.click(screen.getByRole('button', { name: 'Mostrar las contraseñas' }));
    expect(screen.getByLabelText('Contraseña inicial')).toHaveAttribute('type', 'text');
    expect(screen.getByLabelText('Repetir contraseña')).toHaveAttribute('type', 'text');

    await userEvent.click(screen.getByRole('button', { name: 'Ocultar las contraseñas' }));
    expect(screen.getByLabelText('Contraseña inicial')).toHaveAttribute('type', 'password');
  });

  it('crea el alumno, avisa y vacía el formulario', async () => {
    vi.mocked(api.registrarUsuario).mockResolvedValue({ id: 'a-2', message: 'ok' });
    mostrar();
    await completar();
    await userEvent.click(screen.getByRole('button', { name: 'Crear alumno' }));

    await waitFor(() =>
      expect(vi.mocked(api.registrarUsuario).mock.calls[0]?.[0]).toEqual({
        nombre: 'Ana',
        apellido: 'Paz',
        email: 'ana@a.test',
        password: 'Clave1234',
        rol: 'Alumno',
      }),
    );
    await waitFor(() => expect(mostrarAviso).toHaveBeenCalledWith('exito', expect.stringContaining('Alumno creado')));
    expect(screen.getByLabelText('Nombre')).toHaveValue('');
    expect(screen.getByLabelText('Contraseña inicial')).toHaveValue('');
    expect(screen.getByLabelText('Repetir contraseña')).toHaveValue('');
    expect(screen.getByLabelText('Repetir email')).toHaveValue('');
  });

  it('con el rol Docente crea un docente y lo avisa', async () => {
    vi.mocked(api.registrarUsuario).mockResolvedValue({ id: 'd-2', message: 'ok' });
    mostrar();
    await userEvent.selectOptions(screen.getByLabelText('Rol'), 'Docente');
    await completar();
    await userEvent.click(screen.getByRole('button', { name: 'Crear docente' }));

    await waitFor(() => expect(vi.mocked(api.registrarUsuario).mock.calls[0]?.[0].rol).toBe('Docente'));
    await waitFor(() => expect(mostrarAviso).toHaveBeenCalledWith('exito', expect.stringContaining('Docente creado')));
  });

  it('ante un email repetido (409) lo explica y conserva lo escrito', async () => {
    vi.mocked(api.registrarUsuario).mockRejectedValue(new ApiError(409, null));
    mostrar();
    await completar();
    await userEvent.click(screen.getByRole('button', { name: 'Crear alumno' }));

    expect(await screen.findByText('Ya existe un usuario con ese email.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveValue('ana@a.test');
  });
});
