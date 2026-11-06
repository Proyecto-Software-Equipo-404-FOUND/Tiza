import { useState, type FormEvent, type ReactNode } from 'react';
import { DoorOpen, Hourglass, Flag, PlayCircle } from 'lucide-react';
import { z } from 'zod';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { Spinner } from '@/shared/components/Spinner';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { IndicadorConexion } from '@/shared/realtime/IndicadorConexion';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { useConexionSala } from '@/shared/realtime/useConexionSala';
import { crearConexionSala } from './crearConexionSala';
import { AvisoIntegridad } from './AvisoIntegridad';
import { TarjetaPregunta } from './TarjetaPregunta';
import { useIntegridadFoco } from './useIntegridadFoco';
import { useSalaAlumno } from './useSalaAlumno';

const esquemaPin = z.string().regex(/^\d{6}$/, 'El PIN tiene 6 dígitos');

const circulo = (icono: ReactNode) => (
  <span className='flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-tiza-menta text-tiza-pasto'>
    {icono}
  </span>
);

type Props = {
  /** Solo para pruebas: permite inyectar una conexión falsa */
  conexion?: ConexionSala;
  /** Solo para pruebas: reemplaza la espera entre reintentos */
  esperar?: (ms: number) => Promise<void>;
};

export function PaginaSalaAlumno({ conexion: conexionInyectada, esperar }: Props) {
  // La conexión tiene que ser estable entre renders (ver useConexionSala)
  const [conexion] = useState(() => conexionInyectada ?? crearConexionSala());
  const { estado, reintentar } = useConexionSala(conexion);
  const {
    fase,
    error,
    motivoCierre,
    pregunta,
    respondidas,
    desfaseMs,
    enviando,
    errorEnvio,
    respuestaEnEspera,
    envioCerrado,
    recuperando,
    errorRecuperacion,
    recuperar,
    unirse,
    enviarRespuesta,
  } = useSalaAlumno(conexion, esperar);
  useIntegridadFoco(conexion, {
    activo: fase === 'enCurso',
    preguntaId: pregunta?.preguntaId ?? null,
    puedeEnviar: estado === 'conectado',
  });
  const enSala = fase === 'espera' || fase === 'enCurso';
  const enEsperaDeRed = respuestaEnEspera && (estado !== 'conectado' || recuperando);
  const [pin, setPin] = useState('');
  const [errorPin, setErrorPin] = useState<string | undefined>();
  const [aceptoAviso, setAceptoAviso] = useState(false);
  const [errorAviso, setErrorAviso] = useState<string | undefined>();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    const resultado = esquemaPin.safeParse(pin);
    setErrorPin(resultado.success ? undefined : resultado.error.issues[0].message);
    setErrorAviso(aceptoAviso ? undefined : 'Tenés que aceptar el aviso para entrar.');
    if (!resultado.success || !aceptoAviso) return;
    void unirse(Number(resultado.data));
  };

  return (
    <main className='mx-auto flex max-w-xl flex-col gap-6 p-6'>
      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Sala en vivo</h1>
        <p className='mt-1 text-tiza-bosque/70'>Entrá a la sala de tu evaluación con el PIN de tu docente.</p>
      </header>

      <IndicadorConexion estado={estado} onReintentar={reintentar} />

      {enSala && estado === 'reconectando' && (
        <p className='text-sm text-tiza-bosque/80'>
          No cierres esta pestaña. Las respuestas que ya enviaste están guardadas y, cuando vuelva la conexión, te
          llevamos a la pregunta en curso.
        </p>
      )}
      {enSala && estado === 'desconectado' && (
        <p className='text-sm text-tiza-bosque/80'>
          Si «Reintentar» no funciona, recargá la página y entrá de nuevo con el PIN.
        </p>
      )}
      {recuperando && <Spinner texto='Recuperando tu sala…' />}
      {enSala && errorRecuperacion && (
        <BannerError mensaje={errorRecuperacion} onReintentar={() => void recuperar()} />
      )}

      {(fase === 'ingreso' || fase === 'uniendo') && (
        <Tarjeta
          titulo='Entrar a una sala'
          subtitulo='Ingresá el PIN de 6 dígitos.'
          icono={circulo(<DoorOpen className='h-6 w-6' aria-hidden='true' />)}
        >
          <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
            <Campo
              etiqueta='PIN'
              inputMode='numeric'
              autoComplete='off'
              maxLength={6}
              placeholder='123456'
              value={pin}
              // Solo dígitos: se descarta cualquier otro carácter al escribir o pegar
              onChange={(evento) => setPin(evento.target.value.replace(/\D/g, ''))}
              error={errorPin}
              disabled={fase === 'uniendo'}
            />
            <AvisoIntegridad
              aceptado={aceptoAviso}
              onCambiar={(valor) => {
                setAceptoAviso(valor);
                if (valor) setErrorAviso(undefined);
              }}
              error={errorAviso}
              deshabilitado={fase === 'uniendo'}
            />
            {error && <BannerError mensaje={error} />}
            <Boton type='submit' cargando={fase === 'uniendo'} disabled={estado !== 'conectado'}>
              Entrar
            </Boton>
          </form>
        </Tarjeta>
      )}

      {fase === 'espera' && (
        <Tarjeta
          titulo='Estás en la sala'
          subtitulo='Esperá a que tu docente inicie la evaluación.'
          icono={circulo(<Hourglass className='h-6 w-6' aria-hidden='true' />)}
        >
          <p className='text-tiza-bosque/80'>No cierres esta pestaña. Cuando empiece, vas a ver la primera pregunta.</p>
        </Tarjeta>
      )}

      {fase === 'enCurso' && pregunta && (
        <TarjetaPregunta
          key={pregunta.preguntaId}
          pregunta={pregunta}
          respondida={respondidas.includes(pregunta.preguntaId)}
          enviando={enviando}
          // Mientras la respuesta espera la conexión no es un error: se avisa que se enviará sola
          error={enEsperaDeRed ? null : errorEnvio}
          respuestaEnEspera={enEsperaDeRed}
          desfaseMs={desfaseMs}
          cerrada={envioCerrado}
          sinConexion={estado !== 'conectado'}
          onEnviar={(respuesta) => void enviarRespuesta(pregunta.preguntaId, respuesta)}
        />
      )}

      {fase === 'enCurso' && (
        <p className='text-center text-xs text-tiza-bosque/70'>Se registra si salís de esta pantalla.</p>
      )}

      {fase === 'enCurso' && !pregunta && (
        <Tarjeta
          titulo='La evaluación empezó'
          subtitulo='En breve vas a ver la primera pregunta.'
          icono={circulo(<PlayCircle className='h-6 w-6' aria-hidden='true' />)}
        />
      )}

      {fase === 'finalizada' && (
        <Tarjeta
          titulo='Examen entregado'
          subtitulo={
            motivoCierre === 'TiempoAgotado' ? 'Se terminó el tiempo.' : 'Tu docente cerró la sala.'
          }
          icono={circulo(<Flag className='h-6 w-6' aria-hidden='true' />)}
        />
      )}
    </main>
  );
}
