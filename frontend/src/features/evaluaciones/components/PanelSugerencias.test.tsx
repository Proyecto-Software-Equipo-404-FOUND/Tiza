import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { useApuntesParaSugerir } from '../hooks/useApuntesParaSugerir';
import { useCursosParaSugerir } from '../hooks/useCursosParaSugerir';
import { useSugerirPreguntas } from '../hooks/useSugerirPreguntas';
import type { RespuestaSugerencias } from '../sugerencias';
import { PanelSugerencias } from './PanelSugerencias';

vi.mock('../hooks/useCursosParaSugerir', () => ({ useCursosParaSugerir: vi.fn() }));
vi.mock('../hooks/useApuntesParaSugerir', () => ({ useApuntesParaSugerir: vi.fn() }));
vi.mock('../hooks/useSugerirPreguntas', () => ({ useSugerirPreguntas: vi.fn() }));

const respuesta: RespuestaSugerencias = {
  materialId: 'm1',
  proveedor: 'mock',
  modelo: 'mock-1',
  revisadaPorAgenteRevisor: true,
  preguntas: [
    {
      enunciado: 'Primera sugerida',
      tipo: 'OpcionMultiple',
      puntajeMaximo: 1,
      opciones: [
        { texto: 'A', esCorrecta: true },
        { texto: 'B', esCorrecta: false },
      ],
      fragmentoApunte: 'Fragmento del apunte',
    },
    { enunciado: 'Segunda sugerida', tipo: 'Abierta', puntajeMaximo: 2 },
  ],
};

function simular(resultado: { respuesta?: RespuestaSugerencias; error?: unknown } = { respuesta }) {
  const pedido = vi.fn();

  vi.mocked(useCursosParaSugerir).mockReturnValue({
    isPending: false,
    isError: false,
    data: { items: [{ id: 'c1', nombre: 'Programación I' }] },
  } as unknown as ReturnType<typeof useCursosParaSugerir>);

  vi.mocked(useApuntesParaSugerir).mockReturnValue({
    isPending: false,
    isError: false,
    isSuccess: true,
    data: [
      { id: 'm1', titulo: 'Unidad 1', estado: 'Listo' },
      { id: 'm2', titulo: 'Unidad 2', estado: 'Procesando' },
    ],
  } as unknown as ReturnType<typeof useApuntesParaSugerir>);

  // Imita lo que hace la mutación real: guarda el resultado o el error y llama a onSuccess
  function useSugerirFalso() {
    const [data, setData] = useState<RespuestaSugerencias | undefined>();
    const [error, setError] = useState<unknown>(null);
    return {
      mutate: (datos: unknown, opciones?: { onSuccess?: (r: RespuestaSugerencias | undefined) => void }) => {
        pedido(datos);
        if (resultado.error) {
          setError(resultado.error);
          return;
        }
        setError(null);
        setData(resultado.respuesta);
        opciones?.onSuccess?.(resultado.respuesta);
      },
      reset: () => {
        setData(undefined);
        setError(null);
      },
      isPending: false,
      isError: error !== null,
      error,
      data,
    } as unknown as ReturnType<typeof useSugerirPreguntas>;
  }
  vi.mocked(useSugerirPreguntas).mockImplementation(useSugerirFalso);

  return pedido;
}

function mostrar() {
  const onAgregar = vi.fn();
  const onCerrar = vi.fn();
  const onEnvioDelForm = vi.fn((evento: { preventDefault: () => void }) => evento.preventDefault());
  render(
    <form onSubmit={onEnvioDelForm}>
      <PanelSugerencias onAgregar={onAgregar} onCerrar={onCerrar} deshabilitado={false} />
      <button type='submit'>Guardar del formulario</button>
    </form>,
  );
  return { onAgregar, onCerrar, onEnvioDelForm };
}

const elegirCursoYApunte = async () => {
  await userEvent.selectOptions(screen.getByLabelText('Curso'), 'c1');
  await userEvent.selectOptions(screen.getByLabelText('Apunte'), 'm1');
};
const pedir = () => userEvent.click(screen.getByRole('button', { name: 'Pedir sugerencias' }));

describe('PanelSugerencias', () => {
  it('en el selector solo ofrece los apuntes en estado Listo', () => {
    simular();
    mostrar();
    expect(screen.getByRole('option', { name: 'Unidad 1' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'Unidad 2' })).not.toBeInTheDocument();
  });

  it('sin curso ni apunte no pide y marca los campos', async () => {
    const pedido = simular();
    mostrar();
    await pedir();

    expect(screen.getByText('Elegí un curso', { selector: 'p' })).toBeInTheDocument();
    expect(screen.getByText('Elegí un apunte', { selector: 'p' })).toBeInTheDocument();
    expect(pedido).not.toHaveBeenCalled();
  });

  it('valida la cantidad y los tipos antes de pedir', async () => {
    const pedido = simular();
    mostrar();
    await elegirCursoYApunte();

    const cantidad = screen.getByLabelText('Cantidad de preguntas');
    await userEvent.clear(cantidad);
    await userEvent.type(cantidad, '11');
    await userEvent.click(screen.getByLabelText('Opción múltiple'));
    await userEvent.click(screen.getByLabelText('Abierta'));
    await pedir();

    expect(screen.getByText('Ingresá un número entero entre 1 y 10')).toBeInTheDocument();
    expect(screen.getByText('Elegí al menos un tipo de pregunta')).toBeInTheDocument();
    expect(pedido).not.toHaveBeenCalled();
  });

  it('pide con los datos elegidos y sin tema si está vacío', async () => {
    const pedido = simular();
    mostrar();
    await elegirCursoYApunte();
    await pedir();

    expect(pedido).toHaveBeenCalledWith({ materialId: 'm1', cantidad: 5, tipos: ['OpcionMultiple', 'Abierta'] });
  });

  it('manda el tema sin espacios y solo los tipos tildados', async () => {
    const pedido = simular();
    mostrar();
    await elegirCursoYApunte();
    await userEvent.click(screen.getByLabelText('Abierta'));
    await userEvent.type(screen.getByLabelText('Tema (opcional)'), '  Bucles  ');
    await pedir();

    expect(pedido).toHaveBeenCalledWith({
      materialId: 'm1',
      cantidad: 5,
      tipos: ['OpcionMultiple'],
      tema: 'Bucles',
    });
  });

  it('muestra las sugerencias con su fragmento, todas tildadas', async () => {
    simular();
    mostrar();
    await elegirCursoYApunte();
    await pedir();

    expect(screen.getByText('Primera sugerida')).toBeInTheDocument();
    expect(screen.getByText(/Fragmento del apunte/)).toBeInTheDocument();
    expect(screen.getByLabelText('Agregar la pregunta sugerida 1')).toBeChecked();
    expect(screen.getByRole('button', { name: 'Agregar las 2 elegidas' })).toBeInTheDocument();
  });

  it('agrega solo las elegidas, como preguntas del formulario, y cierra el panel', async () => {
    simular();
    const { onAgregar, onCerrar } = mostrar();
    await elegirCursoYApunte();
    await pedir();

    await userEvent.click(screen.getByLabelText('Agregar la pregunta sugerida 1'));
    await userEvent.click(screen.getByRole('button', { name: 'Agregar la pregunta elegida' }));

    expect(onAgregar).toHaveBeenCalledTimes(1);
    const [agregadas] = onAgregar.mock.calls[0];
    expect(agregadas).toHaveLength(1);
    expect(agregadas[0]).toMatchObject({ enunciado: 'Segunda sugerida', tipo: 'Abierta', puntajeMaximo: '2' });
    expect(onCerrar).toHaveBeenCalledTimes(1);
  });

  it('si destildás todas, no se puede agregar', async () => {
    simular();
    mostrar();
    await elegirCursoYApunte();
    await pedir();

    await userEvent.click(screen.getByLabelText('Agregar la pregunta sugerida 1'));
    await userEvent.click(screen.getByLabelText('Agregar la pregunta sugerida 2'));
    expect(screen.getByRole('button', { name: 'Agregar las 0 elegidas' })).toBeDisabled();
  });

  it('si la IA no devuelve preguntas, lo avisa', async () => {
    simular({ respuesta: { ...respuesta, preguntas: [] } });
    mostrar();
    await elegirCursoYApunte();
    await pedir();

    expect(screen.getByText(/La IA no devolvió preguntas/)).toBeInTheDocument();
  });

  it('ante un 503 muestra el mensaje y permite reintentar', async () => {
    const pedido = simular({
      error: new ApiError(503, { status: 503, type: 'IA_NO_DISPONIBLE', title: 'x' }),
    });
    mostrar();
    await elegirCursoYApunte();
    await pedir();

    expect(screen.getByText('La IA no está disponible. Probá de nuevo en unos minutos.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(pedido).toHaveBeenCalledTimes(2);
  });

  it('un apunte sin texto no ofrece reintentar', async () => {
    simular({ error: new ApiError(422, { status: 422, type: 'MATERIAL_SIN_TEXTO', title: 'x' }) });
    mostrar();
    await elegirCursoYApunte();
    await pedir();

    expect(screen.getByText(/no tiene texto para generar preguntas/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
  });

  it('al cambiar de curso se limpian las sugerencias y el apunte', async () => {
    simular();
    mostrar();
    await elegirCursoYApunte();
    await pedir();
    expect(screen.getByText('Primera sugerida')).toBeInTheDocument();

    await userEvent.selectOptions(screen.getByLabelText('Curso'), '');
    expect(screen.queryByText('Primera sugerida')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Apunte')).toHaveValue('');
  });

  it('el Enter en un campo no envía el formulario del examen', async () => {
    simular();
    const { onEnvioDelForm } = mostrar();
    await userEvent.type(screen.getByLabelText('Tema (opcional)'), 'Bucles{Enter}');
    expect(onEnvioDelForm).not.toHaveBeenCalled();
  });

  it('cerrar avisa al formulario', async () => {
    simular();
    const { onCerrar } = mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Cerrar' }));
    expect(onCerrar).toHaveBeenCalledTimes(1);
  });
});
