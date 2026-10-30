import { Plus, Trash2 } from 'lucide-react';
import { Boton } from '@/shared/components/Boton';
import {
  nuevaOpcion,
  nuevoCriterio,
  sumaCriterios,
  type ErroresBorrador,
  type PreguntaBorrador,
} from '../borrador';

type Props = {
  pregunta: PreguntaBorrador;
  numero: number;
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

export function EditorPregunta({ pregunta, numero, errores, deshabilitado, onCambiar, onQuitar }: Props) {
  const cambiar = (cambios: Partial<PreguntaBorrador>) => onCambiar({ ...pregunta, ...cambios });
  const esOpcionMultiple = pregunta.tipo === 'OpcionMultiple';
  const suma = sumaCriterios(pregunta.criterios);
  const maximo = Number(pregunta.puntajeMaximo);

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

      <div className='flex flex-col gap-1.5'>
        <label htmlFor={`enunciado-${pregunta.id}`} className='text-sm font-semibold text-tiza-bosque'>
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

      <div className='grid gap-4 sm:grid-cols-2'>
        <div className='flex flex-col gap-1.5'>
          <label htmlFor={`tipo-${pregunta.id}`} className='text-sm font-semibold text-tiza-bosque'>
            Tipo de la pregunta {numero}
          </label>
          <select
            id={`tipo-${pregunta.id}`}
            value={pregunta.tipo}
            onChange={(evento) => cambiar({ tipo: evento.target.value as PreguntaBorrador['tipo'] })}
            disabled={deshabilitado}
            className={entrada(false)}
          >
            <option value='OpcionMultiple'>Opción múltiple</option>
            <option value='Abierta'>Abierta</option>
          </select>
        </div>

        <div className='flex flex-col gap-1.5'>
          <label htmlFor={`puntaje-${pregunta.id}`} className='text-sm font-semibold text-tiza-bosque'>
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

      {esOpcionMultiple ? (
        <fieldset className='flex flex-col gap-2'>
          <legend className='mb-1 text-sm font-semibold text-tiza-bosque'>Opciones (marcá la correcta)</legend>
          {pregunta.opciones.map((opcion, indice) => (
            <div key={opcion.id} className='flex flex-col gap-1'>
              <div className='flex items-center gap-2'>
                <input
                  type='radio'
                  name={`correcta-${pregunta.id}`}
                  checked={opcion.esCorrecta}
                  onChange={() =>
                    cambiar({
                      opciones: pregunta.opciones.map((o) => ({ ...o, esCorrecta: o.id === opcion.id })),
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
                  onClick={() => cambiar({ opciones: pregunta.opciones.filter((o) => o.id !== opcion.id) })}
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
      ) : (
        <fieldset className='flex flex-col gap-2'>
          <legend className='mb-1 text-sm font-semibold text-tiza-bosque'>Rúbrica (opcional)</legend>
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
                  aria-label={`Criterio ${indice + 1} de la pregunta ${numero}`}
                  aria-invalid={errores[`${criterio.clave}.descripcion`] ? true : undefined}
                  className={entrada(Boolean(errores[`${criterio.clave}.descripcion`]))}
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
                  aria-label={`Puntaje del criterio ${indice + 1} de la pregunta ${numero}`}
                  aria-invalid={errores[`${criterio.clave}.puntaje`] ? true : undefined}
                  className={`${entrada(Boolean(errores[`${criterio.clave}.puntaje`]))} !w-28 shrink-0`}
                />
                <Boton
                  type='button'
                  variante='secundario'
                  className={botonIcono}
                  onClick={() => cambiar({ criterios: pregunta.criterios.filter((c) => c.clave !== criterio.clave) })}
                  disabled={deshabilitado}
                  aria-label={`Quitar el criterio ${indice + 1} de la pregunta ${numero}`}
                >
                  <Trash2 className='h-4 w-4' aria-hidden='true' />
                </Boton>
              </div>
              <MensajeError texto={errores[`${criterio.clave}.descripcion`] ?? errores[`${criterio.clave}.puntaje`]} />
            </div>
          ))}
          {pregunta.criterios.length > 0 && (
            <p className='text-sm text-tiza-bosque/70'>
              Los criterios suman {suma} de {Number.isFinite(maximo) ? maximo : 0} puntos.
            </p>
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
