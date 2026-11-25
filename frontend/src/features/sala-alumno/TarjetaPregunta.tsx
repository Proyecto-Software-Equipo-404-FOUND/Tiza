import { CheckCircle2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { Cronometro, useTiempoRestante } from './Cronometro';
import { QuizBucketSort } from './componentes/QuizBucketSort';
import { QuizClasificadorRapido } from './componentes/QuizClasificadorRapido';
import { QuizEmparejamiento } from './componentes/QuizEmparejamiento';
import { QuizOrdenamiento } from './componentes/QuizOrdenamiento';
import type { PreguntaActual } from './tipos';

const MAX_RESPUESTA = 10000;

type Props = {
  pregunta: PreguntaActual;
  respondida: boolean;
  enviando: boolean;
  error: string | null;
  desfaseMs: number;
  /* El servidor ya no acepta respuestas a esta pregunta (tiempo, sala cerrada, sesión reemplazada…) */
  cerrada?: boolean;
  /* Sin conexión con la sala: se puede seguir escribiendo, pero no enviar hasta reconectar */
  sinConexion?: boolean;
  /* La respuesta ya se intentó enviar y se reenvía sola cuando vuelva la conexión */
  respuestaEnEspera?: boolean;
  onEnviar: (respuesta: string) => void;
};

export function TarjetaPregunta({
  pregunta,
  respondida,
  enviando,
  error,
  desfaseMs,
  cerrada = false,
  sinConexion = false,
  respuestaEnEspera = false,
  onEnviar,
}: Props) {
  const [respuesta, setRespuesta] = useState('');
  const restante = useTiempoRestante(pregunta.fechaLimiteUtc, desfaseMs);
  const agotado = restante <= 0 || cerrada;

  const titulo = `Pregunta ${pregunta.indice} de ${pregunta.totalPreguntas}`;

  if (respondida) {
    return (
      <Tarjeta
        titulo={titulo}
        subtitulo='Tu respuesta fue enviada.'
        icono={
          <span className='flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-tiza-pasto'>
            <CheckCircle2 className='h-6 w-6' aria-hidden='true' />
          </span>
        }
      >
        <p className='text-tiza-bosque/80'>
          {pregunta.indice < pregunta.totalPreguntas
            ? 'Esperá a que tu docente pase a la próxima pregunta.'
            : 'Era la última pregunta. Esperá a que tu docente cierre la sala.'}
        </p>
      </Tarjeta>
    );
  }

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (respuesta.trim() && !enviando && !agotado && !sinConexion) onEnviar(respuesta);
  };

  /* Renderiza el cuerpo de la pregunta aplicando el patrón Strategy según pregunta.tipo */
  const renderizarCuerpoPregunta = () => {
    const deshabilitado = enviando || agotado || sinConexion;

    switch (pregunta.tipo) {
      case 'BucketSort':
        return (
          <QuizBucketSort
            cestas={pregunta.cestas ?? []}
            items={pregunta.itemsBucket ?? []}
            puntajePorItem={pregunta.puntajePorItem}
            deshabilitado={deshabilitado}
            onCambioRespuesta={(serializado, completado) => setRespuesta(completado ? serializado : '')}
          />
        );

      case 'ClasificadorRapido':
        return (
          <QuizClasificadorRapido
            afirmaciones={pregunta.afirmaciones ?? []}
            puntajePorItem={pregunta.puntajePorItem}
            deshabilitado={deshabilitado}
            onCambioRespuesta={(serializado, completado) => setRespuesta(completado ? serializado : '')}
          />
        );

      case 'Emparejamiento':
        return (
          <QuizEmparejamiento
            pares={pregunta.pares ?? []}
            puntajePorItem={pregunta.puntajePorItem}
            deshabilitado={deshabilitado}
            onCambioRespuesta={(serializado, completado) => setRespuesta(completado ? serializado : '')}
          />
        );

      case 'Ordenamiento':
        return (
          <QuizOrdenamiento
            items={pregunta.itemsOrden ?? []}
            puntajeMaximo={pregunta.puntajeMaximo}
            deshabilitado={deshabilitado}
            onCambioRespuesta={(serializado, completado) => setRespuesta(completado ? serializado : '')}
          />
        );

      case 'OpcionMultiple':
        return (
          <div role='radiogroup' aria-labelledby='enunciado' className='flex flex-col gap-2'>
            {(pregunta.opciones ?? []).map((opcion) => (
              <label
                key={opcion.id}
                className={`flex cursor-pointer items-center gap-3 rounded-xl border px-4 py-3 ${
                  respuesta === opcion.id ? 'border-tiza-pasto bg-tiza-menta/60' : 'border-tiza-borde bg-white'
                }`}
              >
                <input
                  type='radio'
                  name='opcion'
                  value={opcion.id}
                  checked={respuesta === opcion.id}
                  onChange={() => setRespuesta(opcion.id)}
                  disabled={deshabilitado}
                  className='h-4 w-4 accent-tiza-pasto'
                />
                {opcion.texto}
              </label>
            ))}
          </div>
        );

      case 'Abierta':
      default:
        return (
          <div className='flex flex-col gap-1.5'>
            <label htmlFor='respuesta-abierta' className='text-sm font-semibold text-tiza-bosque'>
              Tu respuesta
            </label>
            <textarea
              id='respuesta-abierta'
              rows={6}
              maxLength={MAX_RESPUESTA}
              value={respuesta}
              onChange={(evento) => setRespuesta(evento.target.value)}
              disabled={deshabilitado}
              className='rounded-xl border border-tiza-borde bg-white px-4 py-3 text-tiza-bosque disabled:cursor-not-allowed disabled:opacity-50'
            />
            <span className='self-end text-xs text-tiza-bosque/60'>
              {respuesta.length} / {MAX_RESPUESTA}
            </span>
          </div>
        );
    }
  };

  return (
    <Tarjeta titulo={titulo} subtitulo={`${pregunta.puntajeMaximo} puntos`}>
      <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
        {/* En el celular el cronómetro va arriba del enunciado para no quitarle ancho */}
        <div className='flex flex-col-reverse gap-3 sm:flex-row sm:items-start sm:justify-between'>
          <p id='enunciado' className='min-w-0 flex-1 break-words font-heading text-xl font-extrabold text-tiza-bosque'>
            {pregunta.enunciado}
          </p>
          <div className='shrink-0 self-end sm:self-auto'>
            <Cronometro limiteUtc={pregunta.fechaLimiteUtc} desfaseMs={desfaseMs} />
          </div>
        </div>

        {renderizarCuerpoPregunta()}

        {error && <BannerError mensaje={error} />}
        {agotado && !error && <BannerError mensaje='Se terminó el tiempo para responder.' />}

        {respuestaEnEspera && !agotado && (
          <p role='status' className='text-sm text-tiza-bosque/80'>
            Tu respuesta se enviará cuando vuelva la conexión.
          </p>
        )}
        {sinConexion && !respuestaEnEspera && !agotado && (
          <p role='status' className='text-sm text-tiza-bosque/80'>
            Sin conexión. Podés seguir escribiendo: vas a poder enviar cuando se recupere.
          </p>
        )}

        <Boton type='submit' cargando={enviando} disabled={!respuesta.trim() || agotado || sinConexion}>
          Enviar respuesta
        </Boton>
      </form>
    </Tarjeta>
  );
}
