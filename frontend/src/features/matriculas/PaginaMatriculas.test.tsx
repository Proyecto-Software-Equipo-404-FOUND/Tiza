import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from '@/shared/api/cliente';
import { AvisosProvider } from '@/shared/components/Aviso';
import { PaginaMatriculas } from './PaginaMatriculas';
import * as api from './api/matriculas';

vi.mock('./api/matriculas');

const alumnos = [
  { id: 'a1', nombre: 'Alumno 1' },
  { id: 'a2', nombre: 'Alumno 2' },
];
// Como responde el servidor: trae el id del alumno pero no su nombre
const matricula = {
  id: 'm1',
  institucionId: 'i1',
  cohorteId: 'c1',
  alumnoId: 'a1',
  fechaAlta: '2026-10-31T12:00:00Z',
  estado: 'Activa',
};

function mostrarPagina() {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={cliente}>
      <AvisosProvider>
        <MemoryRouter initialEntries={['/cohortes/c1/matriculas']}>
          <Routes>
            <Route path='/cohortes/:cohorteId/matriculas' element={<PaginaMatriculas />} />
          </Routes>
        </MemoryRouter>
      </AvisosProvider>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(api.listarAlumnos).mockResolvedValue(alumnos);
});

describe('PaginaMatriculas', () => {
  it('muestra el título y el enlace para volver a los cursos', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    mostrarPagina();

    expect(await screen.findByRole('heading', { level: 1, name: 'Matrícula' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '← Volver a cursos' })).toHaveAttribute(
      'href',
      '/cursos',
    );
  });

  it('no envía dos veces mientras hay un envío en curso', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    vi.mocked(api.matricularAlumno).mockReturnValue(new Promise(() => {}));
    mostrarPagina();

    await userEvent.selectOptions(await screen.findByLabelText('Alumno'), 'a2');
    const formulario = screen.getByRole('button', { name: 'Matricular' }).closest('form')!;
    fireEvent.submit(formulario);
    fireEvent.submit(formulario);

    await waitFor(() => expect(api.matricularAlumno).toHaveBeenCalledTimes(1));
  });

  it('lista los matriculados y no ofrece a los que ya lo están', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([matricula]);
    mostrarPagina();

    expect(await screen.findByRole('cell', { name: 'Alumno 1' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Alumno 1' })).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Alumno 2' })).toBeInTheDocument();
  });

  it('muestra el estado vacío', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    mostrarPagina();

    expect(
      await screen.findByText('Todavía no hay alumnos matriculados en esta cohorte.'),
    ).toBeInTheDocument();
  });

  it('muestra un error si no se pueden cargar los datos', async () => {
    vi.mocked(api.listarMatriculas).mockRejectedValue(new Error('x'));
    mostrarPagina();

    expect(await screen.findByText('Algo salió mal. Probá de nuevo.')).toBeInTheDocument();
  });

  it('mantiene el botón deshabilitado hasta elegir un alumno', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    mostrarPagina();

    await screen.findByLabelText('Alumno');
    expect(screen.getByRole('button', { name: 'Matricular' })).toBeDisabled();
  });

  it('matricula al alumno elegido y muestra el aviso de éxito', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    vi.mocked(api.matricularAlumno).mockResolvedValue({ ...matricula, alumnoId: 'a2' });
    mostrarPagina();

    await userEvent.selectOptions(await screen.findByLabelText('Alumno'), 'a2');
    await userEvent.click(screen.getByRole('button', { name: 'Matricular' }));

    await waitFor(() => expect(api.matricularAlumno).toHaveBeenCalledWith('c1', 'a2'));
    expect(await screen.findByText('Alumno matriculado correctamente.')).toBeInTheDocument();
  });

  it('muestra el mensaje de conflicto ante un 409', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    vi.mocked(api.matricularAlumno).mockRejectedValue(
      new ApiError(409, { status: 409, title: 'Conflicto' }),
    );
    mostrarPagina();

    await userEvent.selectOptions(await screen.findByLabelText('Alumno'), 'a1');
    await userEvent.click(screen.getByRole('button', { name: 'Matricular' }));

    expect(
      await screen.findByText('Alguien modificó este dato. Recargá la página e intentá de nuevo.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });

  it('muestra el mensaje de permiso ante un 403', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    vi.mocked(api.matricularAlumno).mockRejectedValue(
      new ApiError(403, { status: 403, title: 'Prohibido' }),
    );
    mostrarPagina();

    await userEvent.selectOptions(await screen.findByLabelText('Alumno'), 'a1');
    await userEvent.click(screen.getByRole('button', { name: 'Matricular' }));

    const mensajes = await screen.findAllByText('No tenés permiso para hacer esto.');
    expect(mensajes.length).toBeGreaterThan(0);
  });

  it('muestra el mensaje de no encontrado ante un 404', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([]);
    vi.mocked(api.matricularAlumno).mockRejectedValue(
      new ApiError(404, { status: 404, title: 'No encontrado' }),
    );
    mostrarPagina();

    await userEvent.selectOptions(await screen.findByLabelText('Alumno'), 'a1');
    await userEvent.click(screen.getByRole('button', { name: 'Matricular' }));

    const mensajes = await screen.findAllByText('No se encontró lo que buscás.');
    expect(mensajes.length).toBeGreaterThan(0);
  });

  it('muestra el nombre del alumno aunque la matrícula solo traiga su id, y la fecha de alta', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([matricula]);
    mostrarPagina();

    expect(await screen.findByRole('cell', { name: 'Alumno 1' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: '31/10/2026' })).toBeInTheDocument();
  });

  it('si el alumno no está en la lista muestra una referencia corta y no queda vacío', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([{ ...matricula, alumnoId: 'xxxx-desconocido' }]);
    mostrarPagina();

    expect(await screen.findByRole('cell', { name: 'Alumno …nocido' })).toBeInTheDocument();
  });

  it('si falla la lista de alumnos igual muestra los matriculados y ofrece reintentar', async () => {
    vi.mocked(api.listarMatriculas).mockResolvedValue([matricula]);
    vi.mocked(api.listarAlumnos).mockRejectedValue(new ApiError(404, { status: 404 }));
    mostrarPagina();

    expect(await screen.findByText(/No pudimos cargar la lista de alumnos/)).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Alumno …a1' })).toBeInTheDocument();
    expect(screen.getByLabelText('Alumno')).toBeDisabled();

    vi.mocked(api.listarAlumnos).mockResolvedValue(alumnos);
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(await screen.findByRole('cell', { name: 'Alumno 1' })).toBeInTheDocument();
    expect(screen.getByLabelText('Alumno')).toBeEnabled();
  });
});
