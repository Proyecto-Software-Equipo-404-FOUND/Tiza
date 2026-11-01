import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ComponentProps } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '@/shared/api/cliente';
import { borradorVacio, type EvaluacionBorrador } from '../borrador';
import { FormularioEvaluacion } from './FormularioEvaluacion';

function mostrar(cambios: Partial<ComponentProps<typeof FormularioEvaluacion>> = {}) {
  const onEnviar = vi.fn();
  const onCancelar = vi.fn();
  render(
    <FormularioEvaluacion
      inicial={borradorVacio()}
      textoEnviar='Guardar borrador'
      enviando={false}
      error={null}
      onEnviar={onEnviar}
      onCancelar={onCancelar}
      {...cambios}
    />,
  );
  return { onEnviar, onCancelar };
}

const agregarPregunta = () => userEvent.click(screen.getByRole('button', { name: 'Agregar pregunta' }));
const guardar = () => userEvent.click(screen.getByRole('button', { name: 'Guardar borrador' }));

describe('FormularioEvaluacion', () => {
  it('sin título no envía y marca el campo', async () => {
    const { onEnviar } = mostrar();
    await guardar();

    expect(screen.getByText('Ingresá un título')).toBeInTheDocument();
    expect(screen.getByText('Revisá los campos marcados antes de guardar.')).toBeInTheDocument();
    expect(onEnviar).not.toHaveBeenCalled();
  });

  it('se puede guardar un borrador sin preguntas', async () => {
    const { onEnviar } = mostrar();
    await userEvent.type(screen.getByLabelText('Título'), '  Parcial 2  ');
    await userEvent.selectOptions(screen.getByLabelText('Tipo'), 'Quiz');
    await guardar();

    expect(onEnviar).toHaveBeenCalledWith({
      titulo: 'Parcial 2',
      tipo: 'Quiz',
      preguntasJson: { version: 1, preguntas: [] },
    });
  });

  it('arma una pregunta de opción múltiple y envía el cuerpo del contrato', async () => {
    const { onEnviar } = mostrar();
    await userEvent.type(screen.getByLabelText('Título'), 'Parcial');
    await agregarPregunta();

    await userEvent.type(screen.getByLabelText('Enunciado de la pregunta 1'), '¿Cuánto es 2 + 3?');
    const puntaje = screen.getByLabelText('Puntaje máximo de la pregunta 1');
    await userEvent.clear(puntaje);
    await userEvent.type(puntaje, '2');
    await userEvent.type(screen.getByLabelText('Texto de la opción 1 de la pregunta 1'), '5');
    await userEvent.type(screen.getByLabelText('Texto de la opción 2 de la pregunta 1'), '23');
    await userEvent.click(screen.getByRole('radio', { name: 'La opción 1 de la pregunta 1 es la correcta' }));
    await guardar();

    expect(onEnviar).toHaveBeenCalledTimes(1);
    const datos = onEnviar.mock.calls[0][0];
    expect(datos.preguntasJson.preguntas).toEqual([
      {
        id: expect.any(String),
        enunciado: '¿Cuánto es 2 + 3?',
        tipo: 'OpcionMultiple',
        puntajeMaximo: 2,
        opciones: [
          { id: expect.any(String), texto: '5', esCorrecta: true },
          { id: expect.any(String), texto: '23', esCorrecta: false },
        ],
      },
    ]);
  });

  it('exige marcar la opción correcta', async () => {
    const { onEnviar } = mostrar();
    await userEvent.type(screen.getByLabelText('Título'), 'Parcial');
    await agregarPregunta();
    await userEvent.type(screen.getByLabelText('Enunciado de la pregunta 1'), 'Pregunta');
    await userEvent.type(screen.getByLabelText('Texto de la opción 1 de la pregunta 1'), 'A');
    await userEvent.type(screen.getByLabelText('Texto de la opción 2 de la pregunta 1'), 'B');
    await guardar();

    expect(screen.getByText('Marcá cuál es la opción correcta')).toBeInTheDocument();
    expect(onEnviar).not.toHaveBeenCalled();
  });

  it('arma una pregunta abierta con rúbrica y muestra cuánto suman los criterios', async () => {
    const { onEnviar } = mostrar();
    await userEvent.type(screen.getByLabelText('Título'), 'TP');
    await agregarPregunta();
    await userEvent.selectOptions(screen.getByLabelText('Tipo de la pregunta 1'), 'Abierta');
    await userEvent.type(screen.getByLabelText('Enunciado de la pregunta 1'), 'Explicá una variable');
    const puntaje = screen.getByLabelText('Puntaje máximo de la pregunta 1');
    await userEvent.clear(puntaje);
    await userEvent.type(puntaje, '5');

    await userEvent.click(screen.getByRole('button', { name: 'Agregar criterio a la pregunta 1' }));
    await userEvent.type(screen.getByLabelText('Criterio 1 de la pregunta 1'), 'Define bien');
    await userEvent.type(screen.getByLabelText('Puntaje del criterio 1 de la pregunta 1'), '3');
    expect(screen.getByText('Los criterios suman 3 de 5 puntos.')).toBeInTheDocument();

    await guardar();
    const [pregunta] = onEnviar.mock.calls[0][0].preguntasJson.preguntas;
    expect(pregunta).toMatchObject({
      tipo: 'Abierta',
      puntajeMaximo: 5,
      rubrica: { criterios: [{ descripcion: 'Define bien', puntaje: 3 }] },
    });
    expect(pregunta).not.toHaveProperty('opciones');
  });

  it('se pueden agregar opciones, pero nunca quedan menos de 2', async () => {
    mostrar();
    await agregarPregunta();
    expect(screen.getByRole('button', { name: 'Quitar la opción 1 de la pregunta 1' })).toBeDisabled();

    await userEvent.click(screen.getByRole('button', { name: 'Agregar opción a la pregunta 1' }));
    expect(screen.getByLabelText('Texto de la opción 3 de la pregunta 1')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Quitar la opción 3 de la pregunta 1' }));
    expect(screen.queryByLabelText('Texto de la opción 3 de la pregunta 1')).not.toBeInTheDocument();
  });

  it('se puede quitar una pregunta', async () => {
    mostrar();
    await agregarPregunta();
    await userEvent.click(screen.getByRole('button', { name: 'Quitar la pregunta 1' }));
    expect(screen.getByText('Todavía no agregaste preguntas.')).toBeInTheDocument();
  });

  it('al editar carga los datos y conserva un tipo que no está en la lista', () => {
    const inicial: EvaluacionBorrador = { titulo: 'Parcial viejo', tipo: 'Parcial', preguntas: [] };
    mostrar({ inicial, textoEnviar: 'Guardar cambios' });
    expect(screen.getByLabelText('Título')).toHaveValue('Parcial viejo');
    expect(screen.getByLabelText('Tipo')).toHaveValue('Parcial');
  });

  it('ante un 409 ofrece «Recargar» y no «Reintentar»', async () => {
    const onRecargar = vi.fn();
    mostrar({ error: new ApiError(409, { status: 409, title: 'Conflicto' }), onRecargar });

    expect(screen.getByText(/Alguien modificó este dato/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Recargar' }));
    expect(onRecargar).toHaveBeenCalledTimes(1);
  });

  it('muestra el detalle de un 400 del servidor', () => {
    mostrar({ error: new ApiError(400, { status: 400, detail: 'La evaluación no es válida.' }) });
    expect(screen.getByText('La evaluación no es válida.')).toBeInTheDocument();
  });

  it('mientras envía deshabilita el botón', () => {
    mostrar({ enviando: true });
    expect(screen.getByRole('button', { name: 'Cargando…' })).toBeDisabled();
  });

  it('cancelar avisa sin enviar', async () => {
    const { onCancelar, onEnviar } = mostrar();
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(onCancelar).toHaveBeenCalledTimes(1);
    expect(onEnviar).not.toHaveBeenCalled();
  });
});
