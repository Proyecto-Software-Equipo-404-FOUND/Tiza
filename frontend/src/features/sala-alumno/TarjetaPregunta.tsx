import { CheckCircle2 } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { Cronometro, useTiempoRestante } from './Cronometro';
import type { PreguntaActual } from './tipos';

const MAX_RESPUESTA = 10000;

type Props = {
  pregunta: PreguntaActual;
  respondida: boolean;
  enviando: boolean;
  error: string | null;
  desfaseMs: number;
  onEnviar: (respuesta: string) => void;
};

export function TarjetaPregunta({ pregunta, respondida, enviando, error, desfaseMs, onEnviar }: Props) {
  const [respuesta, setRespuesta] = useState('');
  const restante = useTiempoRestante(pregunta.fechaLimiteUtc, desfaseMs);
  const agotado = restante <= 0;

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
    if (respuesta.trim() && !enviando && !agotado) onEnviar(respuesta);
  };

  return (
    <Tarjeta titulo={titulo} subtitulo={`${pregunta.puntajeMaximo} puntos`}>
      <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
        <div className='flex items-start justify-between gap-3'>
          <p id='enunciado' className='font-heading text-xl font-extrabold text-tiza-bosque'>
            {pregunta.enunciado}
          </p>
          <Cronometro limiteUtc={pregunta.fechaLimiteUtc} desfaseMs={desfaseMs} />
        </div>

        {pregunta.tipo === 'OpcionMultiple' ? (
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
                  disabled={enviando || agotado}
                  className='h-4 w-4 accent-tiza-pasto'
                />
                {opcion.texto}
              </label>
            ))}
          </div>
        ) : (
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
              disabled={enviando || agotado}
              className='rounded-xl border border-tiza-borde bg-white px-4 py-3 text-tiza-bosque disabled:cursor-not-allowed disabled:opacity-50'
            />
            <span className='self-end text-xs text-tiza-bosque/60'>
              {respuesta.length} / {MAX_RESPUESTA}
            </span>
          </div>
        )}

        {error && <BannerError mensaje={error} />}
        {agotado && !error && <BannerError mensaje='Se terminó el tiempo para responder.' />}

        <Boton type='submit' cargando={enviando} disabled={!respuesta.trim() || agotado}>
          Enviar respuesta
        </Boton>
      </form>
    </Tarjeta>
  );
}
