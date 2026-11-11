import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import { FormularioApunte } from './FormularioApunte';
import { useSubirMaterial } from '../hooks/useSubirMaterial';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { TAMANO_MAXIMO_BYTES, validarArchivo } from '../apuntes';
import { CURSO_1_ID } from '@/mocks/datos';

vi.mock('../hooks/useSubirMaterial', () => ({ useSubirMaterial: vi.fn() }));
vi.mock('@/shared/components/Aviso', () => ({ useAviso: vi.fn() }));

function simular(estado: Record<string, unknown> = {}) {
  const mutate = vi.fn();
  const mostrar = vi.fn();
  vi.mocked(useSubirMaterial).mockReturnValue({
    mutate,
    isPending: false,
    ...estado,
  } as unknown as ReturnType<typeof useSubirMaterial>);
  vi.mocked(useAviso).mockReturnValue({ mostrar } as unknown as ReturnType<typeof useAviso>);
  return { mutate, mostrar };
}

function mostrarFormulario() {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <FormularioApunte cursoId={CURSO_1_ID} />
    </QueryClientProvider>,
  );
}

const pdf = () => new File(['contenido'], 'unidad-3.pdf', { type: 'application/pdf' });

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
