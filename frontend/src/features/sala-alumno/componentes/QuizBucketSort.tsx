import { useState, type DragEvent } from 'react';
import type { CestaPregunta, ItemBucketPregunta } from '../tipos';

/*
 * Componente interactivo para clasificación en cestas (Bucket Sort).
 * Soporta arrastre nativo con HTML5 Drag & Drop y toque móvil (Tap-to-Assign)
 * para asegurar máxima accesibilidad durante pruebas en smartphones.
 */

type Props = {
  cestas: CestaPregunta[];
  items: ItemBucketPregunta[];
  puntajePorItem?: number;
  deshabilitado: boolean;
  onCambioRespuesta: (serializado: string, completado: boolean) => void;
};

export function QuizBucketSort({
  cestas,
  items,
  puntajePorItem = 0.3,
  deshabilitado,
  onCambioRespuesta,
}: Props) {
  /* Mapa de asignaciones: itemId -> cestaId */
  const [asignaciones, setAsignaciones] = useState<Record<string, string>>({});
  /* Ítem actualmente seleccionado para interacción táctil en dispositivos móviles */
  const [itemSeleccionado, setItemSeleccionado] = useState<string | null>(null);

  /* Actualiza el mapa de asignaciones y notifica si todos los ítems fueron clasificados */
  const actualizarAsignacion = (itemId: string, destinoCesta: string) => {
    if (deshabilitado) return;
    const nuevas = { ...asignaciones, [itemId]: destinoCesta };
    setAsignaciones(nuevas);
    setItemSeleccionado(null);

    const todosAsignados = items.length > 0 && items.every((it) => Boolean(nuevas[it.id]));
    if (todosAsignados) {
      onCambioRespuesta(JSON.stringify({ tipo: 'BucketSort', asignaciones: nuevas }), true);
    } else {
      onCambioRespuesta('', false);
    }
  };

  /* Remueve una ficha de su cesta y la retorna al mazo de pendientes */
  const regresarAlMazo = (itemId: string) => {
    if (deshabilitado) return;
    const nuevas = { ...asignaciones };
    delete nuevas[itemId];
    setAsignaciones(nuevas);
    onCambioRespuesta('', false);
  };

  /* Manejo de eventos HTML5 Drag & Drop nativo */
  const manejarDragStart = (evento: DragEvent<HTMLDivElement>, itemId: string) => {
    if (deshabilitado) return;
    evento.dataTransfer.setData('text/plain', itemId);
  };

  const manejarDrop = (evento: DragEvent<HTMLDivElement>, cestaId: string) => {
    evento.preventDefault();
    if (deshabilitado) return;
    const itemId = evento.dataTransfer.getData('text/plain');
    if (itemId) actualizarAsignacion(itemId, cestaId);
  };

  const permitirDrop = (evento: DragEvent<HTMLDivElement>) => {
    evento.preventDefault();
  };

  const itemsPorClasificar = items.filter((it) => !asignaciones[it.id]);

  return (
    <div className='flex flex-col gap-4 select-none'>
      {/* Contenedores de cestas de destino */}
      <div className='grid grid-cols-1 gap-3 sm:grid-cols-2'>
        {cestas.map((cesta) => {
          const itemsEnCesta = items.filter((it) => asignaciones[it.id] === cesta.id);

          return (
            <div
              key={cesta.id}
              data-testid={`cesta-${cesta.id}`}
              onDragOver={permitirDrop}
              onDrop={(evento) => manejarDrop(evento, cesta.id)}
              onClick={() => itemSeleccionado && actualizarAsignacion(itemSeleccionado, cesta.id)}
              className='flex min-h-[120px] flex-col justify-between rounded-2xl border-2 border-dashed border-tiza-pasto/40 bg-tiza-menta/30 p-3 transition-colors hover:bg-tiza-menta/50'
            >
              <div className='flex items-center justify-between border-b border-tiza-pasto/20 pb-2'>
                <span className='flex items-center gap-1.5 font-heading text-sm font-extrabold text-tiza-bosque'>
                  <span aria-hidden='true'>{cesta.icono}</span>
                  {cesta.titulo}
                </span>
                <span
                  data-testid={`contador-cesta-${cesta.id}`}
                  className='rounded-full bg-tiza-pasto px-2 py-0.5 text-xs font-bold text-white'
                >
                  {itemsEnCesta.length}
                </span>
              </div>

              {/* Fichas asignadas a esta cesta */}
              <div className='flex flex-wrap gap-1.5 py-2'>
                {itemsEnCesta.map((item) => (
                  <button
                    key={item.id}
                    type='button'
                    title='Tocá para regresar al mazo'
                    disabled={deshabilitado}
                    onClick={(evento) => {
                      evento.stopPropagation();
                      regresarAlMazo(item.id);
                    }}
                    className='flex items-center gap-1 rounded-lg bg-tiza-bosque px-2.5 py-1 text-xs font-semibold text-white shadow-sm transition-all hover:bg-tiza-coral'
                  >
                    <span>{item.texto}</span>
                    <span className='text-[10px] opacity-70' aria-hidden='true'>✕</span>
                  </button>
                ))}
                {itemsEnCesta.length === 0 && (
                  <p className='m-auto text-xs italic text-tiza-bosque/50'>
                    Arrastrá o tocá una ficha aquí
                  </p>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {/* Mazo inferior de fichas pendientes */}
      <div className='rounded-2xl border border-tiza-borde bg-tiza-fondo/70 p-3'>
        <div className='mb-2 flex items-center justify-between'>
          <span className='text-xs font-bold uppercase tracking-wider text-tiza-bosque/70'>
            Fichas disponibles ({itemsPorClasificar.length})
          </span>
          <span className='text-xs text-tiza-bosque/60'>
            {puntajePorItem} pts c/u · Total: {Math.round(items.length * puntajePorItem * 100) / 100} pts
          </span>
        </div>

        <div className='flex flex-wrap gap-2'>
          {itemsPorClasificar.map((item) => {
            const estaActivo = itemSeleccionado === item.id;

            return (
              <div
                key={item.id}
                role='button'
                tabIndex={0}
                data-testid={`ficha-${item.id}`}
                draggable={!deshabilitado}
                onDragStart={(evento) => manejarDragStart(evento, item.id)}
                onClick={() => setItemSeleccionado(estaActivo ? null : item.id)}
                onKeyDown={(evento) => {
                  if (evento.key === 'Enter' || evento.key === ' ') {
                    setItemSeleccionado(estaActivo ? null : item.id);
                  }
                }}
                className={`cursor-grab active:cursor-grabbing rounded-xl border px-3 py-1.5 text-xs font-semibold shadow-sm transition-all ${
                  estaActivo
                    ? 'border-tiza-bosque bg-tiza-lima text-tiza-bosque ring-2 ring-tiza-bosque'
                    : 'border-tiza-borde bg-white text-tiza-bosque hover:border-tiza-pasto'
                }`}
              >
                {item.texto}
              </div>
            );
          })}
          {itemsPorClasificar.length === 0 && (
            <span className='text-xs font-semibold text-tiza-pasto'>
              ¡Todas las fichas fueron clasificadas!
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
