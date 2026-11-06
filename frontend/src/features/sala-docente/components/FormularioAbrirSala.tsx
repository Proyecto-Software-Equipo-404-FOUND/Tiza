import { DoorOpen } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { CAPACIDAD_MAXIMA, CAPACIDAD_MINIMA, CAPACIDAD_POR_DEFECTO, validarCapacidad } from '../api/salas';

type Props = {
  cargando: boolean;
  /** Mensaje de error del servidor al intentar abrir la sala */
  error?: string | null;
  onAbrir: (capacidadMaxima: number) => void;
};

export function FormularioAbrirSala({ cargando, error, onAbrir }: Props) {
  const [texto, setTexto] = useState(String(CAPACIDAD_POR_DEFECTO));
  const [errorCampo, setErrorCampo] = useState<string | undefined>();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (cargando) return;
    const { valor, error: invalido } = validarCapacidad(texto);
    if (valor === null) {
      setErrorCampo(invalido);
      return;
    }
    setErrorCampo(undefined);
    onAbrir(valor);
  };

  return (
    <Tarjeta titulo='Abrir sala'>
      <form onSubmit={enviar} noValidate className='flex flex-col gap-4'>
        <p className='text-tiza-bosque/80'>
          Se crea una sala con un PIN para que los alumnos entren. Vence a las 2 horas.
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
        {error && <BannerError mensaje={error} />}
        <Boton type='submit' cargando={cargando} className='inline-flex items-center justify-center gap-2'>
          <DoorOpen className='h-5 w-5' aria-hidden='true' />
          Abrir sala
        </Boton>
      </form>
    </Tarjeta>
  );
}