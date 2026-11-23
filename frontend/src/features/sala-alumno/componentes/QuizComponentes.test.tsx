import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { QuizBucketSort } from './QuizBucketSort';
import { QuizClasificadorRapido } from './QuizClasificadorRapido';
import { QuizEmparejamiento } from './QuizEmparejamiento';
import { QuizOrdenamiento } from './QuizOrdenamiento';

describe('QuizBucketSort', () => {
  const cestas = [
    { id: 'c1', titulo: 'Clean Architecture', icono: '🏛️' },
    { id: 'c2', titulo: 'Patrones GoF', icono: '🧩' },
  ];
  const items = [
    { id: 'i1', texto: 'Entidades', cestaCorrecta: 'c1' },
    { id: 'i2', texto: 'Singleton', cestaCorrecta: 'c2' },
  ];

  it('clasifica ítems por toque y emite completado al asignar todas las fichas', () => {
    const onCambio = vi.fn();
    render(<QuizBucketSort cestas={cestas} items={items} deshabilitado={false} onCambioRespuesta={onCambio} />);

    expect(screen.getByText('Fichas disponibles (2)')).toBeInTheDocument();

    // 1) Tocar ficha i1 y luego cesta c1
    fireEvent.click(screen.getByTestId('ficha-i1'));
    fireEvent.click(screen.getByTestId('cesta-c1'));

    expect(onCambio).toHaveBeenLastCalledWith('', false);
    expect(screen.getByTestId('contador-cesta-c1')).toHaveTextContent('1');

    // 2) Tocar ficha i2 y luego cesta c2
    fireEvent.click(screen.getByTestId('ficha-i2'));
    fireEvent.click(screen.getByTestId('cesta-c2'));

    expect(onCambio).toHaveBeenLastCalledWith(
      JSON.stringify({ tipo: 'BucketSort', asignaciones: { i1: 'c1', i2: 'c2' } }),
      true,
    );
    expect(screen.getByText('¡Todas las fichas fueron clasificadas!')).toBeInTheDocument();
  });
});

describe('QuizClasificadorRapido', () => {
  const afirmaciones = [
    { id: 'a1', texto: 'El dominio depende de la infraestructura', esVerdadera: false },
    { id: 'a2', texto: 'SRP indica una única razón para cambiar', esVerdadera: true },
  ];

  it('avanza por las afirmaciones y completa la ronda emitiendo el mapa de respuestas', () => {
    const onCambio = vi.fn();
    render(
      <QuizClasificadorRapido
        afirmaciones={afirmaciones}
        deshabilitado={false}
        onCambioRespuesta={onCambio}
      />,
    );

    expect(screen.getByTestId('contador-progreso')).toHaveTextContent('Afirmación 1 de 2');
    expect(screen.getByText('"El dominio depende de la infraestructura"')).toBeInTheDocument();

    // Responder Falso a la 1
    fireEvent.click(screen.getByRole('button', { name: /falso/i }));
    expect(onCambio).toHaveBeenLastCalledWith('', false);

    // Responder Verdadero a la 2
    expect(screen.getByTestId('contador-progreso')).toHaveTextContent('Afirmación 2 de 2');
    fireEvent.click(screen.getByRole('button', { name: /verdadero/i }));

    expect(onCambio).toHaveBeenLastCalledWith(
      JSON.stringify({ tipo: 'ClasificadorRapido', respuestas: { a1: false, a2: true } }),
      true,
    );
    expect(screen.getByText('¡Ronda rápida completada!')).toBeInTheDocument();
  });
});

describe('QuizEmparejamiento', () => {
  const pares = [
    { conceptoId: 'c1', conceptoTexto: 'SRP', definicionId: 'd1', definicionTexto: 'Única responsabilidad' },
    { conceptoId: 'c2', conceptoTexto: 'OCP', definicionId: 'd2', definicionTexto: 'Abierto/Cerrado' },
  ];

  it('asocia conceptos con definiciones por toques cruzados y emite completado', () => {
    const onCambio = vi.fn();
    render(<QuizEmparejamiento pares={pares} deshabilitado={false} onCambioRespuesta={onCambio} />);

    // 1) Seleccionar c1 y luego d1
    fireEvent.click(screen.getByTestId('concepto-c1'));
    fireEvent.click(screen.getByTestId('definicion-d1'));

    expect(onCambio).toHaveBeenLastCalledWith('', false);
    expect(screen.getByTestId('progreso-emparejamiento')).toHaveTextContent('Emparejados: 1 de 2');

    // 2) Seleccionar c2 y luego d2
    fireEvent.click(screen.getByTestId('concepto-c2'));
    fireEvent.click(screen.getByTestId('definicion-d2'));

    expect(onCambio).toHaveBeenLastCalledWith(
      JSON.stringify({ tipo: 'Emparejamiento', pares: { c1: 'd1', c2: 'd2' } }),
      true,
    );
    expect(screen.getByTestId('progreso-emparejamiento')).toHaveTextContent('Emparejados: 2 de 2');
  });
});

describe('QuizOrdenamiento', () => {
  const items = [
    { id: 'step1', texto: 'Paso 1: Routing', ordenCorrecto: 1 },
    { id: 'step2', texto: 'Paso 2: Auth', ordenCorrecto: 2 },
  ];

  it('permite reordenar la secuencia mediante botones de subida/bajada', () => {
    const onCambio = vi.fn();
    render(<QuizOrdenamiento items={items} puntajeMaximo={1} deshabilitado={false} onCambioRespuesta={onCambio} />);

    // Por defecto inicia invertido: [step2, step1]
    expect(onCambio).toHaveBeenCalledWith(
      JSON.stringify({ tipo: 'Ordenamiento', orden: ['step2', 'step1'] }),
      true,
    );

    // Mover step1 hacia arriba
    fireEvent.click(screen.getByTestId('subir-step1'));

    // Ahora el orden debe ser [step1, step2]
    expect(onCambio).toHaveBeenLastCalledWith(
      JSON.stringify({ tipo: 'Ordenamiento', orden: ['step1', 'step2'] }),
      true,
    );
  });
});
