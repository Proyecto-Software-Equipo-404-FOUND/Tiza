import { DoorOpen } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { Tarjeta } from '@/shared/components/Tarjeta';
import {
  CAPACIDAD_MAXIMA,
  CAPACIDAD_MINIMA,
  CAPACIDAD_POR_DEFECTO,
  DURACION_MAXIMA,
  DURACION_MINIMA,
  DURACION_POR_DEFECTO,
  validarCapacidad,
  validarDuracion,
} from '../api/salas';

type Props = {
  cargando: boolean;
  /** Mensaje de error del servidor al intentar abrir la sala */
  error?: string | null;
  onAbrir: (capacidadMaxima: number, duracionMinutos: number) => void;
};

export function FormularioAbrirSala({ cargando, error, onAbrir }: Props) {
  const [texto, setTexto] = useState(String(CAPACIDAD_POR_DEFECTO));
  const [errorCampo, setErrorCampo] = useState<string | undefined>();
  const [textoDuracion, setTextoDuracion] = useState(String(DURACION_POR_DEFECTO));
  const [errorDuracion, setErrorDuracion] = useState<string | undefined>();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (cargando) return;
    const capacidad = validarCapacidad(texto);
    const duracion = validarDuracion(textoDuracion);
    setErrorCampo(capacidad.error);
    setErrorDuracion(duracion.error);
    if (capacidad.valor === null || duracion.valor === null) return;
    onAbrir(capacidad.valor, duracion.valor);
  };

  return (
    <Tarjeta titulo='Abrir sala'>
      <form onSubmit={enviar} noValidate className='flex flex-col gap-4'>
        <p className='text-tiza-bosque/80'>
          Se crea una sala con un PIN para que los alumnos entren. El tiempo empieza a correr cuando la abrís:
          al vencer, ya no se puede entrar ni responder.
        </p>
        <Campo
          etiqueta={`Capacidad máxima (${CAPACIDAD_MINIMA} a ${CAPACIDAD_MAXIMA} alumnos)`}
          type='number'
          inputMode='numeric'
          min={CAPACIDAD_MINIMA}
          max={CAPACIDAD_MAXIMA}
          value={texto}
          error={errorCampo}
          onChange={(evento) => setTexto(evento.target.value)}
        />
        <Campo
          etiqueta={`Duración en minutos (${DURACION_MINIMA} a ${DURACION_MAXIMA})`}
          type='number'
          inputMode='numeric'
          min={DURACION_MINIMA}
          max={DURACION_MAXIMA}
          value={textoDuracion}
          error={errorDuracion}
          onChange={(evento) => setTextoDuracion(evento.target.value)}
        />
        {error && <BannerError mensaje={error} />}
        <Boton type='submit' cargando={cargando} className='inline-flex items-center justify-center gap-2'>
          <DoorOpen className='h-5 w-5' aria-hidden='true' />
          Abrir sala
        </Boton>
      </form>
    </Tarjeta>
  );
}