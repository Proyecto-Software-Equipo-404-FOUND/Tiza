import { useState, useEffect } from 'react';
import { ArrowDown, ArrowUp } from 'lucide-react';
import type { ItemOrdenPregunta } from '../tipos';

/*
 * Componente interactivo para Ordenar Pasos / Secuencia cronológica.
 * Permite reordenar elementos mediante botones de intercambio (swap) accesibles
 * con regla de calificación «Todo o Nada» (1 punto si es 100% exacto, 0 si hay error).
 */

type Props = {
  items: ItemOrdenPregunta[];
  puntajeMaximo?: number;
  deshabilitado: boolean;
  onCambioRespuesta: (serializado: string, completado: boolean) => void;
};

export function QuizOrdenamiento({
  items,
  puntajeMaximo = 1.0,
  deshabilitado,
  onCambioRespuesta,
}: Props) {
  /* Inicializa los ítems alterando el orden si vienen ordenados para exigir la interacción del alumno */
  const [itemsActuales, setItemsActuales] = useState<ItemOrdenPregunta[]>(() => {
    if (items.length <= 1) return [...items];
    /* Invierte el orden inicial para que el usuario deba ordenar */
    return [...items].reverse();
  });

  /* Emite la respuesta inicial al montar */
  useEffect(() => {
    if (itemsActuales.length > 0) {
      onCambioRespuesta(
        JSON.stringify({ tipo: 'Ordenamiento', orden: itemsActuales.map((it) => it.id) }),
        true,
      );
    }
  }, []);

  /* Intercambio posicional seguro (swap) entre el índice actual y el adyacente */
  const mover = (indiceOrigen: number, direccion: -1 | 1) => {
    if (deshabilitado) return;
    const nuevoIndice = indiceOrigen + direccion;
    if (nuevoIndice < 0 || nuevoIndice >= itemsActuales.length) return;

    const copia = [...itemsActuales];
    const temporal = copia[indiceOrigen];
    copia[indiceOrigen] = copia[nuevoIndice];
    copia[nuevoIndice] = temporal;

    setItemsActuales(copia);
    onCambioRespuesta(
      JSON.stringify({ tipo: 'Ordenamiento', orden: copia.map((it) => it.id) }),
      true,
    );
  };

  return (
    <div className='flex flex-col gap-3 select-none'>
      <div className='flex items-center justify-between text-xs font-semibold text-tiza-bosque/70'>
        <span>Ordená los pasos de arriba hacia abajo (1 al {itemsActuales.length})</span>
        <span className='rounded bg-tiza-menta/70 px-2 py-0.5 text-tiza-bosque'>
          Todo o Nada · {puntajeMaximo} pt
        </span>
      </div>

      <div className='flex flex-col gap-2'>
        {itemsActuales.map((item, idx) => (
          <div
            key={item.id}
            data-testid={`item-orden-${item.id}`}
            className='flex items-center justify-between rounded-xl border border-tiza-borde bg-white p-3 shadow-sm transition-all hover:border-tiza-pasto/40'
          >
            <div className='flex items-center gap-3'>
              <span className='flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-tiza-menta font-heading text-xs font-extrabold text-tiza-bosque'>
                {idx + 1}
              </span>
              <span className='text-sm font-medium text-tiza-bosque'>{item.texto}</span>
            </div>

            <div className='flex items-center gap-1'>
              <button
                type='button'
                data-testid={`subir-${item.id}`}
                disabled={deshabilitado || idx === 0}
                onClick={() => mover(idx, -1)}
                aria-label={`Mover "${item.texto}" hacia arriba`}
                className='rounded-lg p-1.5 text-tiza-bosque transition-colors hover:bg-tiza-menta/50 disabled:cursor-not-allowed disabled:opacity-20'
              >
                <ArrowUp className='h-4 w-4' />
              </button>
              <button
                type='button'
                data-testid={`bajar-${item.id}`}
                disabled={deshabilitado || idx === itemsActuales.length - 1}
                onClick={() => mover(idx, 1)}
                aria-label={`Mover "${item.texto}" hacia abajo`}
                className='rounded-lg p-1.5 text-tiza-bosque transition-colors hover:bg-tiza-menta/50 disabled:cursor-not-allowed disabled:opacity-20'
              >
                <ArrowDown className='h-4 w-4' />
              </button>
            </div>
          </div>
        ))}
      </div>

      <p className='text-xs text-tiza-bosque/60'>
        Usá las flechas para reordenar la secuencia correcta. La corrección evalúa la secuencia completa.
      </p>
    </div>
  );
}
