import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { ImportarAlumnosCsv } from './ImportarAlumnosCsv';
import * as api from './api/usuarios';

vi.mock('./api/usuarios');
const mostrarAviso = vi.fn();
vi.mock('@/shared/components/Aviso', () => ({ useAviso: () => ({ mostrar: mostrarAviso }) }));

const csv = () => new File(['nombre,apellido,email\nAna,Paz,ana@a.test'], 'alumnos.csv', { type: 'text/csv' });

function mostrar() {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <ImportarAlumnosCsv />
    </QueryClientProvider>,
  );
}

describe('ImportarAlumnosCsv', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('el botón está deshabilitado hasta elegir un archivo', () => {
    mostrar();
    expect(screen.getByRole('button', { name: 'Importar CSV' })).toBeDisabled();
  });

  it('rechaza un archivo que no es .csv', async () => {
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), new File(['x'], 'alumnos.xlsx'), {
      applyAccept: false,
    });
    expect(screen.getByRole('alert')).toHaveTextContent('Elegí un archivo .csv.');
    expect(screen.getByRole('button', { name: 'Importar CSV' })).toBeDisabled();
  });

  it('envía el archivo y muestra cuántos se crearon, cuántos se omitieron y cuáles fallaron', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({
      creados: 28,
      omitidos: 1,
      errores: [{ fila: 7, motivo: 'Email inválido' }],
    });
    mostrar();
    const archivo = csv();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), archivo);
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('28 alumnos creados.')).toBeInTheDocument();
    expect(vi.mocked(api.importarAlumnos).mock.calls[0]?.[0]).toBe(archivo);
    expect(screen.getByText(/1 fila omitida porque su email ya existía/)).toBeInTheDocument();
    expect(screen.getByText('1 fila falló:')).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: '7' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Email inválido' })).toBeInTheDocument();
    expect(mostrarAviso).toHaveBeenCalledWith('exito', 'Importación terminada.');
  });

  it('cada fila con error muestra lo que dice esa línea del archivo, ordenadas como en el archivo', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({
      creados: 1,
      omitidos: 0,
      errores: [
        { fila: 4, motivo: 'Faltan el nombre o el apellido' },
        { fila: 3, motivo: 'Email inválido' },
      ],
    });
    mostrar();
    const archivo = new File(
      ['nombre,apellido,email\nAna,Paz,ana@a.test\nLuis,Gómez,mal-email\nSofía,,sofia@a.test'],
      'alumnos.csv',
      { type: 'text/csv' },
    );
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), archivo);
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByRole('cell', { name: 'Luis,Gómez,mal-email' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Sofía,,sofia@a.test' })).toBeInTheDocument();
    const filas = screen.getAllByRole('row').slice(1);
    expect(within(filas[0]).getByRole('cell', { name: '3' })).toBeInTheDocument();
    expect(within(filas[1]).getByRole('cell', { name: '4' })).toBeInTheDocument();
    expect(screen.getByText(/la 1 son los títulos/)).toBeInTheDocument();
  });

  it('en singular y sin errores dice que ninguna fila falló', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({ creados: 1, omitidos: 0, errores: [] });
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('1 alumno creado.')).toBeInTheDocument();
    expect(screen.getByText('Ninguna fila falló.')).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('con varios alumnos creados dice «Reciben» y con uno solo «Recibe»', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValueOnce({ creados: 2, omitidos: 0, errores: [] });
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));
    expect(await screen.findByText(/Reciben un email para definir su contraseña/)).toBeInTheDocument();

    vi.mocked(api.importarAlumnos).mockResolvedValueOnce({ creados: 1, omitidos: 0, errores: [] });
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));
    expect(await screen.findByText(/Recibe un email para definir su contraseña/)).toBeInTheDocument();
    expect(screen.queryByText(/Reciben un email/)).not.toBeInTheDocument();
  });

  it('muestra un ejemplo de cómo armar el archivo y aclara el máximo de alumnos', () => {
    mostrar();
    const ejemplo = screen.getByLabelText('Ejemplo de archivo CSV');
    expect(ejemplo).toHaveTextContent('nombre,apellido,email');
    expect(ejemplo).toHaveTextContent('Ana,Paz,ana.paz@correo.com');
    expect(screen.getByText(/Hasta 200 alumnos por archivo \(sin contar la fila de títulos\)/)).toBeInTheDocument();
    expect(screen.getByText(/separadas por comas/)).toBeInTheDocument();
    expect(screen.getByText(/de hasta 1 MiB/)).toBeInTheDocument();
  });

  it('si no se creó ninguno no muestra el aviso de éxito', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({
      creados: 0,
      omitidos: 0,
      errores: [{ fila: 2, motivo: 'Email inválido' }],
    });
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('0 alumnos creados.')).toBeInTheDocument();
    expect(mostrarAviso).not.toHaveBeenCalled();
  });

  it('muestra el motivo que manda el servidor en el title cuando el archivo es inválido (400)', async () => {
    vi.mocked(api.importarAlumnos).mockRejectedValue(
      new ApiError(400, { title: 'El encabezado debe ser exactamente nombre,apellido,email.', status: 400 }),
    );
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(
      await screen.findByText('El encabezado debe ser exactamente nombre,apellido,email.'),
    ).toBeInTheDocument();
  });

  it('explica el error 413 (archivo demasiado grande)', async () => {
    vi.mocked(api.importarAlumnos).mockRejectedValue(new ApiError(413, null));
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('El archivo es demasiado grande. El máximo es 1 MiB.')).toBeInTheDocument();
  });

  it('explica el error 415', async () => {
    vi.mocked(api.importarAlumnos).mockRejectedValue(new ApiError(415, null));
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('El archivo tiene que ser un .csv en UTF-8.')).toBeInTheDocument();
  });

  it('sin contraseña inicial no manda ninguna y avisa que reciben un email', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({ creados: 2, omitidos: 0, errores: [] });
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText(/Reciben un email para definir su contraseña/)).toBeInTheDocument();
    expect(vi.mocked(api.importarAlumnos).mock.calls[0]?.[1]).toBeUndefined();
  });

  it('con contraseña inicial la manda, avisa que ya pueden entrar y la limpia al terminar', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({ creados: 2, omitidos: 0, errores: [] });
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.type(screen.getByLabelText('Contraseña inicial (opcional)'), 'clave1234');
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText(/Ya pueden iniciar sesión con la contraseña inicial/)).toBeInTheDocument();
    expect(screen.queryByText(/Reciben un email/)).not.toBeInTheDocument();
    expect(vi.mocked(api.importarAlumnos).mock.calls[0]?.[1]).toBe('clave1234');
    expect(screen.getByLabelText('Contraseña inicial (opcional)')).toHaveValue('');
  });

  it('no manda el archivo si la contraseña inicial es muy corta', async () => {
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.type(screen.getByLabelText('Contraseña inicial (opcional)'), 'corta');
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('Mínimo 8 caracteres')).toBeInTheDocument();
    expect(api.importarAlumnos).not.toHaveBeenCalled();
  });

  it('rechaza una contraseña inicial de más de 72 bytes', async () => {
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.type(screen.getByLabelText('Contraseña inicial (opcional)'), 'ñ'.repeat(40));
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));

    expect(await screen.findByText('Máximo 72 bytes')).toBeInTheDocument();
    expect(api.importarAlumnos).not.toHaveBeenCalled();
  });

  it('permite mostrar y ocultar la contraseña inicial', async () => {
    mostrar();
    const campo = screen.getByLabelText('Contraseña inicial (opcional)');
    expect(campo).toHaveAttribute('type', 'password');
    await userEvent.click(screen.getByRole('button', { name: 'Mostrar la contraseña inicial de la importación' }));
    expect(campo).toHaveAttribute('type', 'text');
  });

  it('elegir otro archivo limpia el resultado anterior', async () => {
    vi.mocked(api.importarAlumnos).mockResolvedValue({ creados: 1, omitidos: 0, errores: [] });
    mostrar();
    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await userEvent.click(screen.getByRole('button', { name: 'Importar CSV' }));
    expect(await screen.findByText('1 alumno creado.')).toBeInTheDocument();

    await userEvent.upload(screen.getByLabelText('Archivo CSV'), csv());
    await waitFor(() => expect(screen.queryByText('1 alumno creado.')).not.toBeInTheDocument());
  });
});
