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
const matricula = {
  id: 'm1',
  cohorteId: 'c1',
  alumnoId: 'a1',
  alumnoNombre: 'Alumno 1',
  estado: 'Activa' as const,
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
    expect(screen.getByRole('link', { name: '← Volver a cursos' })).toHaveAttribute('href', '/cursos');
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
});
