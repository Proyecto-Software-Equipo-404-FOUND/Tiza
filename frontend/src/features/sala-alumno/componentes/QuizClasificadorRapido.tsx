import { useState } from 'react';
import { Check, RotateCcw, X } from 'lucide-react';
import type { AfirmacionPregunta } from '../tipos';

/*
 * Componente interactivo para clasificación rápida bipolar (Flashcard Verdadero/Falso).
 * Permite responder conceptos ágilmente con botones grandes de alta ergonomía táctil
 * y revisar afirmaciones previas antes de realizar el envío definitivo.
 */

type Props = {
  afirmaciones: AfirmacionPregunta[];
  puntajePorItem?: number;
  deshabilitado: boolean;
  onCambioRespuesta: (serializado: string, completado: boolean) => void;
};

export function QuizClasificadorRapido({
  afirmaciones,
  puntajePorItem = 0.3,
  deshabilitado,
  onCambioRespuesta,
}: Props) {
  const [indiceActual, setIndiceActual] = useState(0);
  /* Mapa de respuestas: idAfirmacion -> true (V) | false (F) */
  const [respuestas, setRespuestas] = useState<Record<string, boolean>>({});

  const afirmacion = afirmaciones[indiceActual];
  const totalRespondidas = Object.keys(respuestas).length;
  const todasRespondidas = afirmaciones.length > 0 && totalRespondidas === afirmaciones.length;
  const completado = indiceActual >= afirmaciones.length && todasRespondidas;

  /* Registra la respuesta (V o F) para la afirmación actual y avanza */
  const responder = (valor: boolean) => {
    if (deshabilitado || !afirmacion) return;

    const nuevas = { ...respuestas, [afirmacion.id]: valor };
    setRespuestas(nuevas);

    const estanTodas = afirmaciones.every((af) => nuevas[af.id] !== undefined);
    if (estanTodas) {
      onCambioRespuesta(JSON.stringify({ tipo: 'ClasificadorRapido', respuestas: nuevas }), true);
    } else {
      onCambioRespuesta('', false);
    }

    setIndiceActual((prev) => prev + 1);
  };

  const reiniciarRonda = () => {
    if (deshabilitado) return;
    setRespuestas({});
    setIndiceActual(0);
    onCambioRespuesta('', false);
  };

  if (completado) {
    return (
      <div className='flex flex-col items-center justify-center gap-3 rounded-2xl border border-tiza-pasto/40 bg-tiza-menta/50 p-6 text-center select-none'>
        <span className='flex h-12 w-12 items-center justify-center rounded-full bg-tiza-pasto text-white font-bold text-xl'>
          ✓
        </span>
        <h4 className='font-heading text-base font-extrabold text-tiza-bosque'>
          ¡Ronda rápida completada!
        </h4>
        <p className='text-sm text-tiza-bosque/80'>
          Respondiste las {afirmaciones.length} afirmaciones ({puntajePorItem} pts c/u). Podés enviar tu respuesta o reiniciar la ronda.
        </p>

        <button
          type='button'
          disabled={deshabilitado}
          onClick={reiniciarRonda}
          className='mt-2 flex items-center gap-2 rounded-xl border border-tiza-bosque/20 bg-white px-4 py-2 text-xs font-semibold text-tiza-bosque shadow-sm transition-all hover:bg-tiza-fondo'
        >
          <RotateCcw className='h-3.5 w-3.5' />
          Reiniciar respuestas
        </button>
      </div>
    );
  }

  return (
    <div className='flex flex-col gap-4 select-none'>
      {/* Progreso de la ronda y puntuación */}
      <div className='flex items-center justify-between text-xs font-semibold text-tiza-bosque/70'>
        <span data-testid='contador-progreso'>
          Afirmación {Math.min(indiceActual + 1, afirmaciones.length)} de {afirmaciones.length}
        </span>
        <span>
          {puntajePorItem} pts c/u · Total: {Math.round(afirmaciones.length * puntajePorItem * 100) / 100} pts
        </span>
      </div>

      {/* Selector de píldoras para navegar entre afirmaciones */}
      <div className='flex gap-1.5 overflow-x-auto pb-1'>
        {afirmaciones.map((af, idx) => {
          const respondida = respuestas[af.id] !== undefined;
          const esActual = idx === indiceActual;

          return (
            <button
              key={af.id}
              type='button'
              disabled={deshabilitado}
              onClick={() => setIndiceActual(idx)}
              title={`Ir a afirmación ${idx + 1}`}
              className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg text-xs font-bold transition-all ${
                esActual
                  ? 'bg-tiza-bosque text-white ring-2 ring-tiza-bosque'
                  : respondida
                    ? 'bg-tiza-menta text-tiza-bosque border border-tiza-pasto/40'
                    : 'bg-white text-tiza-bosque/60 border border-tiza-borde'
              }`}
            >
              {idx + 1}
            </button>
          );
        })}
      </div>

      {/* Tarjeta de Afirmación Central */}
      {afirmacion && (
        <div className='flex min-h-[130px] items-center justify-center rounded-2xl border border-tiza-borde bg-white p-5 text-center shadow-tarjeta transition-all'>
          <p
            data-testid='texto-afirmacion'
            className='font-heading text-base font-bold text-tiza-bosque leading-snug'
          >
            "{afirmacion.texto}"
          </p>
        </div>
      )}

      {/* Botones de Selección Bipolar */}
      <div className='grid grid-cols-2 gap-3 pt-1'>
        {/* FALSO */}
        <button
          type='button'
          disabled={deshabilitado}
          onClick={() => responder(false)}
          className='flex items-center justify-center gap-2 rounded-xl border-2 border-tiza-coral/40 bg-tiza-coral/10 py-3.5 px-4 font-heading text-sm font-extrabold text-tiza-coral shadow-sm transition-all active:scale-[0.98] hover:bg-tiza-coral/20 disabled:opacity-40'
        >
          <X className='h-5 w-5' aria-hidden='true' />
          Falso
        </button>

        {/* VERDADERO */}
        <button
          type='button'
          disabled={deshabilitado}
          onClick={() => responder(true)}
          className='flex items-center justify-center gap-2 rounded-xl border-2 border-tiza-pasto/40 bg-tiza-menta py-3.5 px-4 font-heading text-sm font-extrabold text-tiza-bosque shadow-sm transition-all active:scale-[0.98] hover:bg-tiza-menta/80 disabled:opacity-40'
        >
          <Check className='h-5 w-5 text-tiza-pasto' aria-hidden='true' />
          Verdadero
        </button>
      </div>
    </div>
  );
}
