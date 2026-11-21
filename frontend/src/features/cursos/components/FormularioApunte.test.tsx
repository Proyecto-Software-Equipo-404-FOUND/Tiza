import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioApunte } from './FormularioApunte';
import { useSubirMaterial } from '../hooks/useSubirMaterial';
import { useGoogleDrivePicker } from '../hooks/useGoogleDrivePicker';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { TAMANO_MAXIMO_BYTES, validarArchivo } from '../apuntes';
import { CURSO_1_ID } from '@/mocks/datos';

vi.mock('../hooks/useSubirMaterial', () => ({ useSubirMaterial: vi.fn() }));
vi.mock('../hooks/useGoogleDrivePicker', () => ({ useGoogleDrivePicker: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: vi.fn() }));

function simular(
  estadoSubir: Record<string, unknown> = {},
  estadoDrive: Record<string, unknown> = {},
) {
  const mutate = vi.fn();
  const mostrar = vi.fn();
  const abrirPicker = vi.fn();

  vi.mocked(useSubirMaterial).mockReturnValue({
    mutate,
    isPending: false,
    ...estadoSubir,
  } as unknown as ReturnType<typeof useSubirMaterial>);

  vi.mocked(useAviso).mockReturnValue({ mostrar } as unknown as ReturnType<typeof useAviso>);

  vi.mocked(useGoogleDrivePicker).mockReturnValue({
    isGoogleDriveConfigurado: false,
    isDescargando: false,
    abrirPicker,
    ...estadoDrive,
  });

  return { mutate, mostrar, abrirPicker };
}

function mostrarFormulario() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <FormularioApunte cursoId={CURSO_1_ID} />
    </QueryClientProvider>,
  );
}

const pdf = (nombre = 'unidad-3.pdf') =>
  new File(['contenido'], nombre, { type: 'application/pdf' });

// fireEvent evita el filtro por `accept` de userEvent.upload
function elegir(archivo: File) {
  fireEvent.change(screen.getByLabelText(/Archivo PDF/), { target: { files: [archivo] } });
}

describe('FormularioApunte', () => {
  it('deshabilita «Subir apunte» mientras no se eligió un archivo', () => {
    simular();
    mostrarFormulario();
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });

  it('si el archivo no es un PDF muestra el error y no deja enviar', () => {
    const { mutate } = simular();
    mostrarFormulario();
    const texto = new File(['hola'], 'notas.txt', { type: 'text/plain' });
    const esperado = validarArchivo(texto);
    expect(esperado).not.toBeNull();

    elegir(texto);

    expect(screen.getByRole('alert')).toHaveTextContent(esperado as string);
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('si el PDF pesa más de 10 MB muestra el error y no deja enviar', () => {
    const { mutate } = simular();
    mostrarFormulario();
    const grande = pdf();
    Object.defineProperty(grande, 'size', { value: TAMANO_MAXIMO_BYTES + 1 });
    const esperado = validarArchivo(grande);
    expect(esperado).not.toBeNull();

    elegir(grande);

    expect(screen.getByRole('alert')).toHaveTextContent(esperado as string);
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
    expect(mutate).not.toHaveBeenCalled();
  });

  it('envía el archivo sin título si se deja vacío', async () => {
    const { mutate } = simular();
    mostrarFormulario();
    const archivo = pdf();

    elegir(archivo);
    await userEvent.click(screen.getByRole('button', { name: 'Subir apunte' }));

    expect(mutate).toHaveBeenCalledWith({ archivo, titulo: '' }, expect.anything());
  });

  it('envía el archivo con el título ingresado', async () => {
    const { mutate } = simular();
    mostrarFormulario();
    const archivo = pdf();

    elegir(archivo);
    await userEvent.type(screen.getByLabelText('Título (opcional)'), 'Unidad 3');
    await userEvent.click(screen.getByRole('button', { name: 'Subir apunte' }));

    expect(mutate).toHaveBeenCalledWith({ archivo, titulo: 'Unidad 3' }, expect.anything());
  });

  it('avisa el éxito y limpia el formulario', async () => {
    const { mutate, mostrar } = simular();
    mutate.mockImplementation((_datos: unknown, opciones: { onSuccess: () => void }) =>
      opciones.onSuccess(),
    );
    mostrarFormulario();

    elegir(pdf());
    await userEvent.type(screen.getByLabelText('Título (opcional)'), 'Unidad 3');
    await userEvent.click(screen.getByRole('button', { name: 'Subir apunte' }));

    expect(mostrar).toHaveBeenCalledWith('exito', 'Apunte subido.');
    expect(screen.getByLabelText('Título (opcional)')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });

  it('muestra tal cual el error del servidor', async () => {
    const { mutate, mostrar } = simular();
    mutate.mockImplementation((_datos: unknown, opciones: { onError: (e: unknown) => void }) =>
      opciones.onError(
        new ApiError(400, {
          title:
            'El PDF no tiene texto seleccionable. Subí un PDF generado desde un procesador de texto, no escaneado.',
          status: 400,
        }),
      ),
    );
    mostrarFormulario();

    elegir(pdf());
    await userEvent.click(screen.getByRole('button', { name: 'Subir apunte' }));

    expect(mostrar).toHaveBeenCalledWith(
      'error',
      'El PDF no tiene texto seleccionable. Subí un PDF generado desde un procesador de texto, no escaneado.',
    );
  });

  it('en un 403 no repite el aviso, porque ya lo muestra el manejo global', async () => {
    const { mutate, mostrar } = simular();
    mutate.mockImplementation((_datos: unknown, opciones: { onError: (e: unknown) => void }) =>
      opciones.onError(
        new ApiError(403, {
          title: 'No tenés permiso para gestionar el material de este curso.',
          status: 403,
        }),
      ),
    );
    mostrarFormulario();

    elegir(pdf());
    await userEvent.click(screen.getByRole('button', { name: 'Subir apunte' }));

    expect(mostrar).not.toHaveBeenCalled();
  });

  it('deshabilita el botón mientras envía', () => {
    simular({ isPending: true });
    mostrarFormulario();
    expect(screen.getByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });
});

describe('FormularioApunte - Integración con Google Drive (S3-CU-05)', () => {
  it('no muestra selector de pestañas si Google Drive no está configurado (degradación agraciada)', () => {
    simular({}, { isGoogleDriveConfigurado: false });
    mostrarFormulario();

    expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: 'Google Drive' })).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Archivo PDF/)).toBeInTheDocument();
  });

  it('muestra las pestañas cuando Google Drive está configurado', () => {
    simular({}, { isGoogleDriveConfigurado: true });
    mostrarFormulario();

    expect(screen.getByRole('tablist')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Desde mi equipo' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(screen.getByRole('tab', { name: 'Google Drive' })).toHaveAttribute(
      'aria-selected',
      'false',
    );
  });

  it('permite cambiar a la pestaña de Google Drive y abrir el selector', async () => {
    const { abrirPicker } = simular({}, { isGoogleDriveConfigurado: true });
    mostrarFormulario();

    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));
    expect(screen.getByRole('tab', { name: 'Google Drive' })).toHaveAttribute(
      'aria-selected',
      'true',
    );

    const botonDrive = screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' });
    await userEvent.click(botonDrive);

    expect(abrirPicker).toHaveBeenCalledTimes(1);
  });

  it('al seleccionar un PDF desde Drive muestra la ficha del archivo y habilita la subida', async () => {
    const { mutate, abrirPicker } = simular({}, { isGoogleDriveConfigurado: true });
    const archivoDrive = pdf('drive-apunte.pdf');

    abrirPicker.mockImplementation(
      (opciones: { onArchivoSeleccionado: (archivo: File) => void }) => {
        opciones.onArchivoSeleccionado(archivoDrive);
      },
    );

    mostrarFormulario();
    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));

    const botonDrive = screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' });
    await userEvent.click(botonDrive);

    expect(screen.getByText('drive-apunte.pdf')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeEnabled();

    await userEvent.click(screen.getByRole('button', { name: 'Subir apunte' }));
    expect(mutate).toHaveBeenCalledWith({ archivo: archivoDrive, titulo: '' }, expect.anything());
  });

  it('cancelar el modal de Google Drive no borra el título ni rompe el formulario', async () => {
    const { abrirPicker } = simular({}, { isGoogleDriveConfigurado: true });
    // Cancelación: abrirPicker no llama a onArchivoSeleccionado ni a onError
    abrirPicker.mockImplementation(() => {});

    mostrarFormulario();
    await userEvent.type(screen.getByLabelText('Título (opcional)'), 'Título Preservado');
    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));

    await userEvent.click(screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' }));

    expect(screen.getByLabelText('Título (opcional)')).toHaveValue('Título Preservado');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });

  it('si falla la conexión o el popup de Google Drive emite un aviso de error flotante', async () => {
    const { abrirPicker, mostrar } = simular({}, { isGoogleDriveConfigurado: true });
    abrirPicker.mockImplementation((opciones: { onError: (msg: string) => void }) => {
      opciones.onError('No se pudo conectar con el servicio de Google Drive.');
    });

    mostrarFormulario();
    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));
    await userEvent.click(screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' }));

    expect(mostrar).toHaveBeenCalledWith(
      'error',
      'No se pudo conectar con el servicio de Google Drive.',
    );
  });

  it('si el archivo de Drive supera los 10 MB muestra el error en línea', async () => {
    const { abrirPicker } = simular({}, { isGoogleDriveConfigurado: true });
    abrirPicker.mockImplementation((opciones: { onError: (msg: string) => void }) => {
      opciones.onError('El PDF pesa más de 10 MB.');
    });

    mostrarFormulario();
    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));
    await userEvent.click(screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' }));

    expect(screen.getByRole('alert')).toHaveTextContent('El PDF pesa más de 10 MB.');
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });

  it('al alternar de pestaña limpia el archivo seleccionado pero preserva el título', async () => {
    simular({}, { isGoogleDriveConfigurado: true });
    mostrarFormulario();

    // Escribir título y seleccionar archivo local
    await userEvent.type(screen.getByLabelText('Título (opcional)'), 'Teoría Cuántica');
    elegir(pdf('apunte-local.pdf'));
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeEnabled();

    // Cambiar a la pestaña de Drive
    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));

    // El archivo debe haberse limpiado, deshabilitando el botón, pero el título queda intacto
    expect(screen.getByLabelText('Título (opcional)')).toHaveValue('Teoría Cuántica');
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });

  it('permite cambiar el archivo elegido de Drive mediante el botón Cambiar archivo', async () => {
    const { abrirPicker } = simular({}, { isGoogleDriveConfigurado: true });
    abrirPicker.mockImplementation(
      (opciones: { onArchivoSeleccionado: (archivo: File) => void }) => {
        opciones.onArchivoSeleccionado(pdf('archivo-1.pdf'));
      },
    );

    mostrarFormulario();
    await userEvent.click(screen.getByRole('tab', { name: 'Google Drive' }));
    await userEvent.click(screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' }));

    expect(screen.getByText('archivo-1.pdf')).toBeInTheDocument();

    // Deseleccionar mediante el botón 'Cambiar archivo'
    await userEvent.click(screen.getByRole('button', { name: 'Cambiar archivo' }));

    expect(screen.queryByText('archivo-1.pdf')).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Elegir archivo desde Google Drive' }),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });

  it('deshabilita el botón de subir mientras se está descargando el archivo de Drive', () => {
    simular({}, { isGoogleDriveConfigurado: true, isDescargando: true });
    mostrarFormulario();

    expect(screen.getByRole('button', { name: 'Subir apunte' })).toBeDisabled();
  });
});
