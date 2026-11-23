import { useState, useMemo } from 'react';
import { Check } from 'lucide-react';
import type { ParPregunta } from '../tipos';

/*
 * Componente interactivo para Unir Conceptos (Matching / Tap-to-Pair).
 * Utiliza dos columnas (Conceptos y Definiciones) con vinculación cromática
 * reactiva mediante toques cruzados, sin requerir librerías pesadas de canvas.
 */

type Props = {
  pares: ParPregunta[];
  puntajePorItem?: number;
  deshabilitado: boolean;
  onCambioRespuesta: (serializado: string, completado: boolean) => void;
};

/* Paleta cromática de pares para diferenciar visualmente cada asociación */
const COLORES_PARES = [
  'bg-emerald-100 border-emerald-500 text-emerald-900',
  'bg-sky-100 border-sky-500 text-sky-900',
  'bg-amber-100 border-amber-500 text-amber-900',
  'bg-purple-100 border-purple-500 text-purple-900',
  'bg-rose-100 border-rose-500 text-rose-900',
];

export function QuizEmparejamiento({
  pares,
  puntajePorItem = 0.3,
  deshabilitado,
  onCambioRespuesta,
}: Props) {
  const [seleccionadoA, setSeleccionadoA] = useState<string | null>(null);
  /* Mapa de pares: conceptoId -> definicionId */
  const [emparejamientos, setEmparejamientos] = useState<Record<string, string>>({});

  const conceptos = useMemo(() => pares.map((p) => ({ id: p.conceptoId, texto: p.conceptoTexto })), [pares]);

  /* Las definiciones se presentan con orden predecible pero desacoplado */
  const definiciones = useMemo(() => {
    const listado = pares.map((p) => ({ id: p.definicionId, texto: p.definicionTexto }));
    /* Desplaza el orden si hay más de 1 ítem para no dejar la respuesta en línea horizontal directa */
    if (listado.length > 2) {
      return [...listado.slice(1), listado[0]];
    }
    return listado;
  }, [pares]);

  /* Asigna un color fijo y consistente a cada par según el orden en que fue emparejado */
  const obtenerEstiloPar = (conceptoId: string) => {
    const claves = Object.keys(emparejamientos);
    const indice = claves.indexOf(conceptoId);
    return indice >= 0 ? COLORES_PARES[indice % COLORES_PARES.length] : '';
  };

  const seleccionarConcepto = (id: string) => {
    if (deshabilitado) return;
    /* Si ya estaba emparejado, tocarlo desasocia el par */
    if (emparejamientos[id]) {
      const nuevos = { ...emparejamientos };
      delete nuevos[id];
      setEmparejamientos(nuevos);
      onCambioRespuesta('', false);
      return;
    }
    setSeleccionadoA(id === seleccionadoA ? null : id);
  };

  const seleccionarDefinicion = (id: string) => {
    if (deshabilitado) return;
    /* Si la definición ya está asignada a otro concepto, desasociarla */
    const conceptoPrevio = Object.keys(emparejamientos).find((key) => emparejamientos[key] === id);
    if (conceptoPrevio) {
      const nuevos = { ...emparejamientos };
      delete nuevos[conceptoPrevio];
      setEmparejamientos(nuevos);
      onCambioRespuesta('', false);
      return;
    }

    /* Si hay un concepto seleccionado esperando su pareja */
    if (seleccionadoA) {
      const nuevos = { ...emparejamientos, [seleccionadoA]: id };
      setEmparejamientos(nuevos);
      setSeleccionadoA(null);

      const todosListos = pares.length > 0 && pares.every((p) => Boolean(nuevos[p.conceptoId]));
      if (todosListos) {
        onCambioRespuesta(JSON.stringify({ tipo: 'Emparejamiento', pares: nuevos }), true);
      } else {
        onCambioRespuesta('', false);
      }
    }
  };

  const totalEmparejados = Object.keys(emparejamientos).length;

  return (
    <div className='flex flex-col gap-4 select-none'>
      <div className='flex items-center justify-between text-xs font-semibold text-tiza-bosque/70'>
        <span data-testid='progreso-emparejamiento'>
          Emparejados: {totalEmparejados} de {pares.length}
        </span>
        <span>
          {puntajePorItem} pts c/u · Total: {Math.round(pares.length * puntajePorItem * 100) / 100} pts
        </span>
      </div>

      <div className='grid grid-cols-1 gap-4 sm:grid-cols-2'>
        {/* Columna Conceptos */}
        <div className='flex flex-col gap-2'>
          <span className='text-xs font-bold uppercase tracking-wider text-tiza-bosque/60'>
            Conceptos
          </span>
          {conceptos.map((item) => {
            const estaEmparejado = Boolean(emparejamientos[item.id]);
            const estaActivo = seleccionadoA === item.id;
            const estiloColor = obtenerEstiloPar(item.id);

            return (
              <button
                key={item.id}
                type='button'
                data-testid={`concepto-${item.id}`}
                disabled={deshabilitado}
                onClick={() => seleccionarConcepto(item.id)}
                className={`flex items-center justify-between rounded-xl border p-3 text-left font-medium transition-all ${
                  estaActivo
                    ? 'border-tiza-pasto ring-2 ring-tiza-pasto/40 bg-tiza-menta/30'
                    : estaEmparejado
                      ? estiloColor
                      : 'border-tiza-borde bg-white hover:border-tiza-pasto/60'
                }`}
              >
                <span className='text-sm'>{item.texto}</span>
                {estaEmparejado && <Check className='h-4 w-4 shrink-0' aria-hidden='true' />}
              </button>
            );
          })}
        </div>

        {/* Columna Definiciones */}
        <div className='flex flex-col gap-2'>
          <span className='text-xs font-bold uppercase tracking-wider text-tiza-bosque/60'>
            Definiciones
          </span>
          {definiciones.map((item) => {
            const conceptoAsociado = Object.keys(emparejamientos).find(
              (k) => emparejamientos[k] === item.id,
            );
            const estaEmparejado = Boolean(conceptoAsociado);
            const estiloColor = conceptoAsociado ? obtenerEstiloPar(conceptoAsociado) : '';

            return (
              <button
                key={item.id}
                type='button'
                data-testid={`definicion-${item.id}`}
                disabled={deshabilitado}
                onClick={() => seleccionarDefinicion(item.id)}
                className={`flex items-center justify-between rounded-xl border p-3 text-left text-sm transition-all ${
                  estaEmparejado
                    ? estiloColor
                    : 'border-tiza-borde bg-white hover:border-tiza-pasto/60'
                }`}
              >
                <span>{item.texto}</span>
                {estaEmparejado && <Check className='h-4 w-4 shrink-0' aria-hidden='true' />}
              </button>
            );
          })}
        </div>
      </div>

      <p className='text-xs text-tiza-bosque/60'>
        Tip: Tocá un concepto y luego su definición correspondiente. Tocá un ítem emparejado para desasociarlo.
      </p>
    </div>
  );
}
