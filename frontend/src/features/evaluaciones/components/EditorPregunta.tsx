import { Plus, Trash2 } from 'lucide-react';
import { Boton } from '@/shared/components/Boton';
import { nuevoUuid } from '@/shared/lib/uuid';
import {
  nuevaOpcion,
  nuevaPregunta,
  nuevoCriterio,
  puntosSinRepartir,
  redondearPuntos,
  sumaCriterios,
  type ErroresBorrador,
  type PreguntaBorrador,
} from '../borrador';
import { etiquetaPuntos } from '../formato';

/*
 * Componente de edición de preguntas para docentes.
 * Filtra los tipos permitidos según la evaluación (en 'Quiz' solo ofrece dinámicas
 * gamificadas y opción múltiple, excluyendo preguntas abiertas).
 * Al seleccionar una dinámica gamificada, autocompleta con una plantilla editable precargada
 * y provee controles numéricos dedicados para editar el puntaje por ítem.
 */

type Props = {
  pregunta: PreguntaBorrador;
  numero: number;
  tipoEvaluacion?: string;
  errores: ErroresBorrador;
  deshabilitado: boolean;
  onCambiar: (pregunta: PreguntaBorrador) => void;
  onQuitar: () => void;
};

const entrada = (hayError: boolean) =>
  `w-full rounded-xl border bg-white px-4 py-3 text-tiza-bosque disabled:cursor-not-allowed disabled:opacity-50 ${
    hayError ? 'border-tiza-coral' : 'border-tiza-borde'
  }`;

function MensajeError({ texto }: { texto?: string }) {
  return texto ? (
    <p role='alert' className='text-sm text-tiza-coral'>
      {texto}
    </p>
  ) : null;
}

const botonIcono = '!px-3 !py-3 shrink-0';

export function EditorPregunta({
  pregunta,
  numero,
  tipoEvaluacion,
  errores,
  deshabilitado,
  onCambiar,
  onQuitar,
}: Props) {
  const cambiar = (cambios: Partial<PreguntaBorrador>) => onCambiar({ ...pregunta, ...cambios });
  const esQuiz = tipoEvaluacion === 'Quiz';

  const suma = redondearPuntos(sumaCriterios(pregunta.criterios));
  const maximo = Number.isFinite(Number(pregunta.puntajeMaximo))
    ? Number(pregunta.puntajeMaximo)
    : 0;
  const diferencia = Number.isFinite(Number(pregunta.puntajeMaximo))
    ? puntosSinRepartir(pregunta)
    : 0;

  /* Manejo de cambio de tipo con plantilla precargada automática */
  const cambiarTipo = (nuevoTipo: PreguntaBorrador['tipo']) => {
    const plantilla = nuevaPregunta(nuevoTipo);
    cambiar({
      ...plantilla,
      id: pregunta.id,
      enunciado: pregunta.enunciado || plantilla.enunciado,
    });
  };

  /* Recalcula el puntaje total al editar el puntaje por ítem */
  const actualizarPuntajePorItem = (nuevoValor: string) => {
    let cantidad = 1;
    if (pregunta.tipo === 'BucketSort') cantidad = pregunta.itemsBucket.length;
    if (pregunta.tipo === 'ClasificadorRapido') cantidad = pregunta.afirmaciones.length;
    if (pregunta.tipo === 'Emparejamiento') cantidad = pregunta.pares.length;

    const totalCalculado = Number.isFinite(Number(nuevoValor))
      ? Math.round(Number(nuevoValor) * cantidad * 100) / 100
      : 0;

    cambiar({
      puntajePorItem: nuevoValor,
      puntajeMaximo: String(totalCalculado),
    });
  };

  return (
    <li className='flex flex-col gap-4 rounded-2xl border border-tiza-borde bg-tiza-fondo/60 p-4'>
      <div className='flex items-center justify-between gap-3'>
        <h3 className='font-heading text-lg font-extrabold text-tiza-bosque'>Pregunta {numero}</h3>
        <Boton
          type='button'
          variante='secundario'
          className={`${botonIcono} !text-tiza-coral`}
          onClick={onQuitar}
          disabled={deshabilitado}
          aria-label={`Quitar la pregunta ${numero}`}
        >
          <Trash2 className='h-4 w-4' aria-hidden='true' />
        </Boton>
      </div>

      {/* Enunciado */}
      <div className='flex flex-col gap-1.5'>
        <label
          htmlFor={`enunciado-${pregunta.id}`}
          className='text-sm font-semibold text-tiza-bosque'
        >
          Enunciado de la pregunta {numero}
        </label>
        <textarea
          id={`enunciado-${pregunta.id}`}
          rows={3}
          value={pregunta.enunciado}
          onChange={(evento) => cambiar({ enunciado: evento.target.value })}
          disabled={deshabilitado}
          aria-invalid={errores[`${pregunta.id}.enunciado`] ? true : undefined}
          className={entrada(Boolean(errores[`${pregunta.id}.enunciado`]))}
        />
        <MensajeError texto={errores[`${pregunta.id}.enunciado`]} />
      </div>

      {/* Selectores de Tipo y Puntajes */}
      <div className='grid gap-4 sm:grid-cols-2'>
        <div className='flex flex-col gap-1.5'>
          <label htmlFor={`tipo-${pregunta.id}`} className='text-sm font-semibold text-tiza-bosque'>
            Tipo de la pregunta {numero}
          </label>
          <select
            id={`tipo-${pregunta.id}`}
            value={pregunta.tipo}
            onChange={(evento) => cambiarTipo(evento.target.value as PreguntaBorrador['tipo'])}
            disabled={deshabilitado}
            className={entrada(false)}
          >
            {esQuiz ? (
              <>
                <option value='OpcionMultiple'>Opción múltiple</option>
                <option value='BucketSort'>Bucket Sort (Clasificación en cestas)</option>
                <option value='ClasificadorRapido'>Clasificador Rápido (Flashcard V/F)</option>
                <option value='Emparejamiento'>Unir conceptos (Matching)</option>
                <option value='Ordenamiento'>Ordenar pasos (Secuencia)</option>
              </>
            ) : (
              <>
                <option value='OpcionMultiple'>Opción múltiple</option>
                <option value='Abierta'>Abierta</option>
              </>
            )}
          </select>
        </div>

        {/* Puntaje Máximo */}
        <div className='flex flex-col gap-1.5'>
          <label
            htmlFor={`puntaje-${pregunta.id}`}
            className='text-sm font-semibold text-tiza-bosque'
          >
            Puntaje máximo de la pregunta {numero}
          </label>
          <input
            id={`puntaje-${pregunta.id}`}
            type='number'
            min={0}
            step='any'
            value={pregunta.puntajeMaximo}
            onChange={(evento) => cambiar({ puntajeMaximo: evento.target.value })}
            disabled={deshabilitado}
            aria-invalid={errores[`${pregunta.id}.puntaje`] ? true : undefined}
            className={entrada(Boolean(errores[`${pregunta.id}.puntaje`]))}
          />
          <MensajeError texto={errores[`${pregunta.id}.puntaje`]} />
        </div>
      </div>

      {/* Control dedicado de puntaje unitario para dinámicas granulares */}
      {(pregunta.tipo === 'BucketSort' ||
        pregunta.tipo === 'ClasificadorRapido' ||
        pregunta.tipo === 'Emparejamiento') && (
        <div className='rounded-xl border border-tiza-borde bg-white p-3 shadow-sm'>
          <div className='flex flex-col gap-1.5 sm:flex-row sm:items-center sm:justify-between'>
            <div>
              <label
                htmlFor={`puntaje-item-${pregunta.id}`}
                className='text-sm font-semibold text-tiza-bosque'
              >
                Puntaje por ítem / acierto (editable)
              </label>
              <p className='text-xs text-tiza-bosque/70'>
                {pregunta.tipo === 'BucketSort' && 'Valor asignado a cada ficha clasificada correctamente.'}
                {pregunta.tipo === 'ClasificadorRapido' && 'Valor asignado a cada afirmación respondida correctamente.'}
                {pregunta.tipo === 'Emparejamiento' && 'Valor asignado a cada par asociado correctamente.'}
              </p>
            </div>
            <div className='flex items-center gap-2'>
              <input
                id={`puntaje-item-${pregunta.id}`}
                type='number'
                min={0}
                step='0.1'
                value={pregunta.puntajePorItem || '0.3'}
                onChange={(evento) => actualizarPuntajePorItem(evento.target.value)}
                disabled={deshabilitado}
                className='w-24 rounded-lg border border-tiza-borde px-3 py-1.5 text-center text-sm font-bold text-tiza-bosque'
              />
              <span className='text-xs font-semibold text-tiza-pasto'>pts c/u</span>
            </div>
          </div>
        </div>
      )}

      {/* ==================== SUB-FORMULARIO: BUCKET SORT ==================== */}
      {pregunta.tipo === 'BucketSort' && (
        <fieldset className='flex flex-col gap-4 rounded-xl border border-tiza-borde bg-white/70 p-4'>
          <legend className='px-1 text-sm font-bold text-tiza-bosque'>
            Configuración de Cestas y Fichas (Bucket Sort)
          </legend>

          {/* Cestas */}
          <div className='flex flex-col gap-2'>
            <span className='text-xs font-bold uppercase tracking-wider text-tiza-bosque/70'>
              Cestas de destino
            </span>
            <div className='grid grid-cols-1 gap-2 sm:grid-cols-2'>
              {pregunta.cestas.map((cesta, idx) => (
                <div key={cesta.id} className='flex items-center gap-2 rounded-xl border border-tiza-borde bg-white p-2'>
                  <input
                    type='text'
                    value={cesta.icono}
                    onChange={(e) =>
                      cambiar({
                        cestas: pregunta.cestas.map((c) =>
                          c.id === cesta.id ? { ...c, icono: e.target.value } : c,
                        ),
                      })
                    }
                    disabled={deshabilitado}
                    title='Icono/Emoji'
                    className='w-10 rounded border border-tiza-borde text-center py-1 text-sm'
                  />
                  <input
                    type='text'
                    value={cesta.titulo}
                    onChange={(e) =>
                      cambiar({
                        cestas: pregunta.cestas.map((c) =>
                          c.id === cesta.id ? { ...c, titulo: e.target.value } : c,
                        ),
                      })
                    }
                    disabled={deshabilitado}
                    placeholder={`Título cesta ${idx + 1}`}
                    className='flex-1 rounded border border-tiza-borde px-2 py-1 text-sm text-tiza-bosque font-medium'
                  />
                </div>
              ))}
            </div>
          </div>

          {/* Fichas */}
          <div className='flex flex-col gap-2'>
            <span className='text-xs font-bold uppercase tracking-wider text-tiza-bosque/70'>
              Fichas a clasificar ({pregunta.itemsBucket.length})
            </span>
            {pregunta.itemsBucket.map((item, idx) => (
              <div key={item.id} className='flex items-center gap-2'>
                <span className='flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-xs font-bold text-tiza-bosque'>
                  {idx + 1}
                </span>
                <input
                  type='text'
                  value={item.texto}
                  onChange={(e) =>
                    cambiar({
                      itemsBucket: pregunta.itemsBucket.map((it) =>
                        it.id === item.id ? { ...it, texto: e.target.value } : it,
                      ),
                    })
                  }
                  disabled={deshabilitado}
                  placeholder='Concepto o ficha'
                  className='flex-1 rounded-lg border border-tiza-borde px-3 py-1.5 text-sm text-tiza-bosque'
                />
                <select
                  value={item.cestaCorrecta}
                  onChange={(e) =>
                    cambiar({
                      itemsBucket: pregunta.itemsBucket.map((it) =>
                        it.id === item.id ? { ...it, cestaCorrecta: e.target.value } : it,
                      ),
                    })
                  }
                  disabled={deshabilitado}
                  className='rounded-lg border border-tiza-borde bg-white px-3 py-1.5 text-xs font-medium text-tiza-bosque'
                >
                  {pregunta.cestas.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.icono} {c.titulo}
                    </option>
                  ))}
                </select>
                <Boton
                  type='button'
                  variante='secundario'
                  className={botonIcono}
                  onClick={() =>
                    cambiar({
                      itemsBucket: pregunta.itemsBucket.filter((it) => it.id !== item.id),
                    })
                  }
                  disabled={deshabilitado || pregunta.itemsBucket.length <= 1}
                  aria-label={`Quitar ficha ${idx + 1}`}
                >
                  <Trash2 className='h-4 w-4' />
                </Boton>
              </div>
            ))}
            <Boton
              type='button'
              variante='secundario'
              className='self-start !px-3 !py-1.5 text-xs'
              onClick={() =>
                cambiar({
                  itemsBucket: [
                    ...pregunta.itemsBucket,
                    { id: nuevoUuid(), texto: '', cestaCorrecta: pregunta.cestas[0]?.id || '' },
                  ],
                })
              }
              disabled={deshabilitado}
            >
              <Plus className='mr-1.5 inline h-4 w-4' />
              Agregar ficha
            </Boton>
          </div>
        </fieldset>
      )}

      {/* ==================== SUB-FORMULARIO: CLASIFICADOR RÁPIDO ==================== */}
      {pregunta.tipo === 'ClasificadorRapido' && (
        <fieldset className='flex flex-col gap-3 rounded-xl border border-tiza-borde bg-white/70 p-4'>
          <legend className='px-1 text-sm font-bold text-tiza-bosque'>
            Afirmaciones para el Clasificador Rápido
          </legend>

          {pregunta.afirmaciones.map((af, idx) => (
            <div key={af.id} className='flex items-center gap-2'>
              <span className='flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-xs font-bold text-tiza-bosque'>
                {idx + 1}
              </span>
              <input
                type='text'
                value={af.texto}
                onChange={(e) =>
                  cambiar({
                    afirmaciones: pregunta.afirmaciones.map((a) =>
                      a.id === af.id ? { ...a, texto: e.target.value } : a,
                    ),
                  })
                }
                disabled={deshabilitado}
                placeholder='Afirmación conceptual'
                className='flex-1 rounded-lg border border-tiza-borde px-3 py-1.5 text-sm text-tiza-bosque'
              />
              <div className='flex items-center gap-1 rounded-lg border border-tiza-borde bg-white p-1'>
                <button
                  type='button'
                  onClick={() =>
                    cambiar({
                      afirmaciones: pregunta.afirmaciones.map((a) =>
                        a.id === af.id ? { ...a, esVerdadera: false } : a,
                      ),
                    })
                  }
                  className={`rounded px-2 py-0.5 text-xs font-bold transition-colors ${
                    !af.esVerdadera ? 'bg-tiza-coral text-white' : 'text-tiza-bosque hover:bg-tiza-fondo'
                  }`}
                >
                  Falsa
                </button>
                <button
                  type='button'
                  onClick={() =>
                    cambiar({
                      afirmaciones: pregunta.afirmaciones.map((a) =>
                        a.id === af.id ? { ...a, esVerdadera: true } : a,
                      ),
                    })
                  }
                  className={`rounded px-2 py-0.5 text-xs font-bold transition-colors ${
                    af.esVerdadera ? 'bg-tiza-pasto text-white' : 'text-tiza-bosque hover:bg-tiza-fondo'
                  }`}
                >
                  Verdadera
                </button>
              </div>
              <Boton
                type='button'
                variante='secundario'
                className={botonIcono}
                onClick={() =>
                  cambiar({
                    afirmaciones: pregunta.afirmaciones.filter((a) => a.id !== af.id),
                  })
                }
                disabled={deshabilitado || pregunta.afirmaciones.length <= 1}
                aria-label={`Quitar afirmación ${idx + 1}`}
              >
                <Trash2 className='h-4 w-4' />
              </Boton>
            </div>
          ))}

          <Boton
            type='button'
            variante='secundario'
            className='self-start !px-3 !py-1.5 text-xs'
            onClick={() =>
              cambiar({
                afirmaciones: [
                  ...pregunta.afirmaciones,
                  { id: nuevoUuid(), texto: '', esVerdadera: true },
                ],
              })
            }
            disabled={deshabilitado}
          >
            <Plus className='mr-1.5 inline h-4 w-4' />
            Agregar afirmación
          </Boton>
        </fieldset>
      )}

      {/* ==================== SUB-FORMULARIO: EMPAREJAMIENTO ==================== */}
      {pregunta.tipo === 'Emparejamiento' && (
        <fieldset className='flex flex-col gap-3 rounded-xl border border-tiza-borde bg-white/70 p-4'>
          <legend className='px-1 text-sm font-bold text-tiza-bosque'>
            Pares de Concepto y Definición (Matching)
          </legend>

          {pregunta.pares.map((par, idx) => (
            <div key={par.id} className='flex items-center gap-2'>
              <span className='flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-xs font-bold text-tiza-bosque'>
                {idx + 1}
              </span>
              <input
                type='text'
                value={par.conceptoTexto}
                onChange={(e) =>
                  cambiar({
                    pares: pregunta.pares.map((p) =>
                      p.id === par.id ? { ...p, conceptoTexto: e.target.value } : p,
                    ),
                  })
                }
                disabled={deshabilitado}
                placeholder='Concepto / Término'
                className='flex-1 rounded-lg border border-tiza-borde px-3 py-1.5 text-sm font-medium text-tiza-bosque'
              />
              <span className='text-xs font-bold text-tiza-bosque/40'>↔</span>
              <input
                type='text'
                value={par.definicionTexto}
                onChange={(e) =>
                  cambiar({
                    pares: pregunta.pares.map((p) =>
                      p.id === par.id ? { ...p, definicionTexto: e.target.value } : p,
                    ),
                  })
                }
                disabled={deshabilitado}
                placeholder='Definición correspondiente'
                className='flex-1 rounded-lg border border-tiza-borde px-3 py-1.5 text-sm text-tiza-bosque'
              />
              <Boton
                type='button'
                variante='secundario'
                className={botonIcono}
                onClick={() =>
                  cambiar({
                    pares: pregunta.pares.filter((p) => p.id !== par.id),
                  })
                }
                disabled={deshabilitado || pregunta.pares.length <= 2}
                aria-label={`Quitar par ${idx + 1}`}
              >
                <Trash2 className='h-4 w-4' />
              </Boton>
            </div>
          ))}

          <Boton
            type='button'
            variante='secundario'
            className='self-start !px-3 !py-1.5 text-xs'
            onClick={() =>
              cambiar({
                pares: [
                  ...pregunta.pares,
                  { id: nuevoUuid(), conceptoTexto: '', definicionTexto: '' },
                ],
              })
            }
            disabled={deshabilitado}
          >
            <Plus className='mr-1.5 inline h-4 w-4' />
            Agregar par
          </Boton>
        </fieldset>
      )}

      {/* ==================== SUB-FORMULARIO: ORDENAMIENTO ==================== */}
      {pregunta.tipo === 'Ordenamiento' && (
        <fieldset className='flex flex-col gap-3 rounded-xl border border-tiza-borde bg-white/70 p-4'>
          <legend className='px-1 text-sm font-bold text-tiza-bosque'>
            Pasos de la Secuencia (en orden cronológico correcto)
          </legend>
          <p className='text-xs text-tiza-bosque/70'>
            Ingresá los pasos en el orden correcto. En el examen del alumno se mostrarán desordenados para evaluar la secuencia.
          </p>

          {pregunta.itemsOrden.map((io, idx) => (
            <div key={io.id} className='flex items-center gap-2'>
              <span className='flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-xs font-bold text-tiza-bosque'>
                {idx + 1}
              </span>
              <input
                type='text'
                value={io.texto}
                onChange={(e) =>
                  cambiar({
                    itemsOrden: pregunta.itemsOrden.map((item) =>
                      item.id === io.id ? { ...item, texto: e.target.value } : item,
                    ),
                  })
                }
                disabled={deshabilitado}
                placeholder={`Paso ${idx + 1}`}
                className='flex-1 rounded-lg border border-tiza-borde px-3 py-1.5 text-sm text-tiza-bosque'
              />
              <Boton
                type='button'
                variante='secundario'
                className={botonIcono}
                onClick={() =>
                  cambiar({
                    itemsOrden: pregunta.itemsOrden.filter((item) => item.id !== io.id),
                  })
                }
                disabled={deshabilitado || pregunta.itemsOrden.length <= 2}
                aria-label={`Quitar paso ${idx + 1}`}
              >
                <Trash2 className='h-4 w-4' />
              </Boton>
            </div>
          ))}

          <Boton
            type='button'
            variante='secundario'
            className='self-start !px-3 !py-1.5 text-xs'
            onClick={() =>
              cambiar({
                itemsOrden: [
                  ...pregunta.itemsOrden,
                  { id: nuevoUuid(), texto: '', ordenCorrecto: pregunta.itemsOrden.length + 1 },
                ],
              })
            }
            disabled={deshabilitado}
          >
            <Plus className='mr-1.5 inline h-4 w-4' />
            Agregar paso
          </Boton>
        </fieldset>
      )}

      {/* ==================== SUB-FORMULARIO: OPCIÓN MÚLTIPLE ==================== */}
      {pregunta.tipo === 'OpcionMultiple' && (
        <fieldset className='flex flex-col gap-2'>
          <legend className='mb-1 text-sm font-semibold text-tiza-bosque'>
            Opciones (marcá la correcta)
          </legend>
          {pregunta.opciones.map((opcion, indice) => (
            <div key={opcion.id} className='flex flex-col gap-1'>
              <div className='flex items-center gap-2'>
                <input
                  type='radio'
                  name={`correcta-${pregunta.id}`}
                  checked={opcion.esCorrecta}
                  onChange={() =>
                    cambiar({
                      opciones: pregunta.opciones.map((o) => ({
                        ...o,
                        esCorrecta: o.id === opcion.id,
                      })),
                    })
                  }
                  disabled={deshabilitado}
                  aria-label={`La opción ${indice + 1} de la pregunta ${numero} es la correcta`}
                  className='h-4 w-4 shrink-0 accent-tiza-pasto'
                />
                <input
                  type='text'
                  value={opcion.texto}
                  onChange={(evento) =>
                    cambiar({
                      opciones: pregunta.opciones.map((o) =>
                        o.id === opcion.id ? { ...o, texto: evento.target.value } : o,
                      ),
                    })
                  }
                  disabled={deshabilitado}
                  aria-label={`Texto de la opción ${indice + 1} de la pregunta ${numero}`}
                  aria-invalid={errores[`${opcion.id}.texto`] ? true : undefined}
                  className={entrada(Boolean(errores[`${opcion.id}.texto`]))}
                />
                <Boton
                  type='button'
                  variante='secundario'
                  className={botonIcono}
                  onClick={() =>
                    cambiar({ opciones: pregunta.opciones.filter((o) => o.id !== opcion.id) })
                  }
                  disabled={deshabilitado || pregunta.opciones.length <= 2}
                  aria-label={`Quitar la opción ${indice + 1} de la pregunta ${numero}`}
                >
                  <Trash2 className='h-4 w-4' aria-hidden='true' />
                </Boton>
              </div>
              <MensajeError texto={errores[`${opcion.id}.texto`]} />
            </div>
          ))}
          <MensajeError texto={errores[`${pregunta.id}.opciones`]} />
          <Boton
            type='button'
            variante='secundario'
            className='self-start !px-3 !py-1.5 text-sm'
            onClick={() => cambiar({ opciones: [...pregunta.opciones, nuevaOpcion()] })}
            disabled={deshabilitado}
          >
            <Plus className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
            Agregar opción a la pregunta {numero}
          </Boton>
        </fieldset>
      )}

      {/* ==================== SUB-FORMULARIO: ABIERTA (CON RÚBRICA) ==================== */}
      {pregunta.tipo === 'Abierta' && (
        <fieldset className='flex flex-col gap-2 rounded-xl border border-dashed border-tiza-borde bg-white/60 p-3'>
          <legend className='px-1 text-sm font-semibold text-tiza-bosque'>
            Rúbrica de corrección (opcional)
          </legend>
          <p className='text-sm text-tiza-bosque/70'>
            Anotá qué esperás encontrar en una buena respuesta y cuántos puntos vale cada cosa. La
            IA la usa para sugerir una nota, pero la nota final siempre la decidís vos. Sin rúbrica
            se corrige solo con el puntaje máximo.
          </p>
          {pregunta.criterios.map((criterio, indice) => (
            <div key={criterio.clave} className='flex flex-col gap-1'>
              <div className='flex items-center gap-2'>
                <input
                  type='text'
                  value={criterio.descripcion}
                  onChange={(evento) =>
                    cambiar({
                      criterios: pregunta.criterios.map((c) =>
                        c.clave === criterio.clave ? { ...c, descripcion: evento.target.value } : c,
                      ),
                    })
                  }
                  disabled={deshabilitado}
                  placeholder='Ej.: Explica el concepto con sus palabras'
                  aria-label={`Criterio ${indice + 1} de la pregunta ${numero}`}
                  aria-invalid={errores[`${criterio.clave}.descripcion`] ? true : undefined}
                  className={`${entrada(Boolean(errores[`${criterio.clave}.descripcion`]))} min-w-0 !py-2 text-sm`}
                />
                <input
                  type='number'
                  min={0}
                  step='any'
                  value={criterio.puntaje}
                  onChange={(evento) =>
                    cambiar({
                      criterios: pregunta.criterios.map((c) =>
                        c.clave === criterio.clave ? { ...c, puntaje: evento.target.value } : c,
                      ),
                    })
                  }
                  disabled={deshabilitado}
                  placeholder='Puntos'
                  aria-label={`Puntaje del criterio ${indice + 1} de la pregunta ${numero}`}
                  aria-invalid={errores[`${criterio.clave}.puntaje`] ? true : undefined}
                  className={`${entrada(Boolean(errores[`${criterio.clave}.puntaje`]))} !w-24 shrink-0 !py-2 text-sm`}
                />
                <Boton
                  type='button'
                  variante='secundario'
                  className={`${botonIcono} !py-2`}
                  onClick={() =>
                    cambiar({
                      criterios: pregunta.criterios.filter((c) => c.clave !== criterio.clave),
                    })
                  }
                  disabled={deshabilitado}
                  aria-label={`Quitar el criterio ${indice + 1} de la pregunta ${numero}`}
                >
                  <Trash2 className='h-4 w-4' aria-hidden='true' />
                </Boton>
              </div>
              <MensajeError
                texto={
                  errores[`${criterio.clave}.descripcion`] ?? errores[`${criterio.clave}.puntaje`]
                }
              />
            </div>
          ))}
          {pregunta.criterios.length > 0 && (
            <p
              className={`text-sm font-semibold ${diferencia !== 0 ? 'text-tiza-coral' : 'text-tiza-pasto'}`}
            >
              Repartiste {suma} de {etiquetaPuntos(maximo)}.{' '}
              {diferencia > 0 &&
                `Te ${diferencia === 1 ? 'falta' : 'faltan'} ${diferencia}: repartilos para poder guardar.`}
              {diferencia < 0 &&
                `Te pasaste por ${-diferencia}: bajá algún puntaje para poder guardar.`}
              {diferencia === 0 && 'Quedó bien repartido.'}
            </p>
          )}
          {diferencia > 0 && pregunta.criterios.length > 0 && (
            <Boton
              type='button'
              variante='secundario'
              className='self-start !px-3 !py-1.5 text-sm'
              onClick={() =>
                cambiar({
                  criterios: pregunta.criterios.map((c, i, todos) =>
                    i === todos.length - 1
                      ? {
                          ...c,
                          puntaje: String(redondearPuntos((Number(c.puntaje) || 0) + diferencia)),
                        }
                      : c,
                  ),
                })
              }
              disabled={deshabilitado}
            >
              Asignar los {diferencia} que faltan al último criterio de la pregunta {numero}
            </Boton>
          )}
          <Boton
            type='button'
            variante='secundario'
            className='self-start !px-3 !py-1.5 text-sm'
            onClick={() => cambiar({ criterios: [...pregunta.criterios, nuevoCriterio()] })}
            disabled={deshabilitado}
          >
            <Plus className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
            Agregar criterio a la pregunta {numero}
          </Boton>
        </fieldset>
      )}
    </li>
  );
}
