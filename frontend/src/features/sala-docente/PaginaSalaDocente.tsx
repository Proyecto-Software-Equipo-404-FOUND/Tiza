import { ArrowLeft, DoorOpen, Play, SkipForward, Square, Users } from 'lucide-react';
import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useSesion } from '@/features/auth/SesionContext';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { ModalConfirmacion } from '@/shared/components/ModalConfirmacion';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { IndicadorConexion } from '@/shared/realtime/IndicadorConexion';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { useConexionSala } from '@/shared/realtime/useConexionSala';
import { guardarSala, olvidarSala, recuperarSala } from './almacenSala';
import { CronometroPanel } from './components/CronometroPanel';
import { FormularioAbrirSala } from './components/FormularioAbrirSala';
import { crearConexionSala } from './crearConexionSala';
import { mensajeAbrirSala } from './errores';
import { useAbrirSala } from './hooks/useAbrirSala';
import { useSalaDocente } from './hooks/useSalaDocente';
import type { SalaCreada, SeveridadFoco } from './tipos';

const ETIQUETA_FOCO: Record<SeveridadFoco, string> = {
  ninguna: 'Sin alertas',
  amarilla: 'Alerta amarilla',
  roja: 'Alerta roja',
};

const ESTILO_FOCO: Record<SeveridadFoco, string> = {
  ninguna: 'bg-tiza-menta text-tiza-pasto',
  amarilla: 'bg-yellow-100 text-yellow-900',
  roja: 'bg-tiza-coral text-white',
};

type PropsPanel = {
  sala: SalaCreada;
  /** Solo para pruebas: permite inyectar una conexión falsa */
  conexion?: ConexionSala;
  onAbrirOtra: () => void;
};

function PanelSala({ sala, conexion: conexionInyectada, onAbrirOtra }: PropsPanel) {
  // La conexión tiene que ser estable entre renders (ver useConexionSala)
  const [conexion] = useState(() => conexionInyectada ?? crearConexionSala());
  const sala_ = useSalaDocente(conexion, sala.id);
  const { estado, reintentar } = useConexionSala(conexion);
  const [modal, setModal] = useState<'finalizar' | 'otra' | null>(null);

  const { fase, recibioEstado, alumnos, totalConectados, pregunta, fechaLimiteSala, desfaseMs, motivoCierre } = sala_;
  const finalizada = fase === 'finalizada';
  // Hasta que llega el primer estadoSala, `fase` es solo el valor inicial: al recargar en plena sala no hay que
  // mostrar la duración quieta. Solo aplica si la sala trae duracionMinutos (una sala sin él ya tiene su fecha límite).
  const esperandoEstado = fase === 'espera' && !recibioEstado && !!sala.duracionMinutos;
  // Hasta que el docente inicia, el reloj no corre: se muestra la duración completa (el fin se fija al iniciar)
  const duracionEnEspera = fase === 'espera' && sala.duracionMinutos ? sala.duracionMinutos * 60 : undefined;
  const limiteDeSala = fechaLimiteSala ?? sala.fechaVencimiento;

  const pedirOtraSala = () => {
    if (finalizada) onAbrirOtra();
    else setModal('otra');
  };

  return (
    <>
      <IndicadorConexion estado={estado} onReintentar={reintentar} />

      <Tarjeta titulo='PIN de la sala' subtitulo='Proyectalo para que los alumnos entren.'>
        <p
          aria-label={`PIN de la sala: ${sala.codigoPIN}`}
          className='text-center font-heading text-6xl font-extrabold tracking-widest text-tiza-bosque sm:text-8xl'
        >
          {sala.codigoPIN}
        </p>
        <p className='mt-2 text-center text-sm text-tiza-bosque/70'>Capacidad máxima: {sala.capacidadMaxima} alumnos</p>
      </Tarjeta>

      {!finalizada && (
        <Tarjeta titulo='Conducción'>
          <div className='flex flex-col gap-4'>
            <div className='flex flex-wrap gap-2'>
              {esperandoEstado ? null : duracionEnEspera !== undefined ? (
                <CronometroPanel etiqueta='Duración' limiteUtc={null} desfaseMs={0} fijoSegundos={duracionEnEspera} />
              ) : (
                <CronometroPanel etiqueta='Sala' limiteUtc={limiteDeSala} desfaseMs={desfaseMs} />
              )}
              {/* Hoy cada pregunta vence junto con la sala: el segundo reloj se muestra solo si difiere */}
              {fase === 'enCurso' && pregunta?.fechaLimiteUtc && pregunta.fechaLimiteUtc !== limiteDeSala && (
                <CronometroPanel etiqueta='Pregunta' limiteUtc={pregunta.fechaLimiteUtc} desfaseMs={desfaseMs} />
              )}
            </div>

            {fase === 'enCurso' && pregunta && (
              <p className='font-bold text-tiza-bosque'>
                Pregunta {pregunta.indice}/{pregunta.totalPreguntas}
                <span className='ml-3 font-normal text-tiza-bosque/80'>
                  Respondieron {sala_.respondieronPreguntaActual} de {totalConectados}
                </span>
              </p>
            )}

            {sala_.error && <BannerError mensaje={sala_.error} />}

            <div className='flex flex-col gap-3 sm:flex-row'>
              {fase === 'espera' && (
                <Boton
                  onClick={() => void sala_.iniciar()}
                  cargando={sala_.trabajando}
                  disabled={sala_.sincronizando || estado !== 'conectado'}
                  className='inline-flex items-center justify-center gap-2'
                >
                  <Play className='h-5 w-5' aria-hidden='true' />
                  Iniciar
                </Boton>
              )}
              {fase === 'enCurso' && (
                <Boton
                  onClick={() => void sala_.siguiente()}
                  cargando={sala_.trabajando}
                  disabled={!sala_.puedeAvanzar || estado !== 'conectado'}
                  className='inline-flex items-center justify-center gap-2'
                >
                  <SkipForward className='h-5 w-5' aria-hidden='true' />
                  {pregunta ? `Siguiente pregunta (${pregunta.indice}/${pregunta.totalPreguntas})` : 'Siguiente pregunta'}
                </Boton>
              )}
              <Boton
                variante='peligro'
                onClick={() => setModal('finalizar')}
                disabled={sala_.trabajando || estado !== 'conectado'}
                className='inline-flex items-center justify-center gap-2'
              >
                <Square className='h-5 w-5' aria-hidden='true' />
                Finalizar
              </Boton>
            </div>
          </div>
        </Tarjeta>
      )}

      {finalizada && (
        <Tarjeta
          titulo='Sala finalizada'
          subtitulo={motivoCierre === 'TiempoAgotado' ? 'Se terminó el tiempo.' : 'Cerraste la sala.'}
        />
      )}

      <Tarjeta titulo='Alumnos'>
        <p className='mb-3 inline-flex items-center gap-2 font-bold text-tiza-bosque'>
          <Users className='h-5 w-5' aria-hidden='true' />
          Conectados ahora: {totalConectados}
        </p>
        {alumnos.length === 0 ? (
          <p className='text-tiza-bosque/70'>Todavía no entró nadie.</p>
        ) : (
          <ul className='flex flex-col gap-2'>
            {alumnos.map((alumno) => (
              <li
                key={alumno.alumnoId}
                className='flex flex-wrap items-center justify-between gap-2 rounded-xl border border-tiza-menta px-3 py-2'
              >
                <span
                  className={`min-w-0 break-words font-bold ${alumno.desconectado ? 'text-tiza-bosque/60' : 'text-tiza-bosque'}`}
                >
                  {alumno.alias}
                </span>
                <span className='flex flex-wrap items-center gap-2'>
                  {alumno.desconectado && (
                    <span className='rounded-full border border-tiza-borde px-3 py-1 text-sm font-bold text-tiza-bosque/70'>
                      Se desconectó
                    </span>
                  )}
                  <span className={`rounded-full px-3 py-1 text-sm font-bold ${ESTILO_FOCO[alumno.peorSeveridad]}`}>
                    {ETIQUETA_FOCO[alumno.peorSeveridad]}
                    {alumno.alertas > 0 ? ` (${alumno.alertas})` : ''}
                  </span>
                </span>
              </li>
            ))}
          </ul>
        )}
        <p className='mt-3 text-xs text-tiza-bosque/70'>
          Si recargás la página, la lista de conectados se recupera sola. Las alertas de foco anteriores no vuelven a mostrarse acá: quedan guardadas y las ves en el detalle de cada entrega. Los que se desconectan quedan en la lista con su aviso.
        </p>
      </Tarjeta>

      <Boton variante='secundario' onClick={pedirOtraSala} className='inline-flex items-center justify-center gap-2'>
        <DoorOpen className='h-5 w-5' aria-hidden='true' />
        Abrir otra sala
      </Boton>

      <ModalConfirmacion
        abierto={modal === 'finalizar'}
        titulo='¿Finalizar la sala?'
        mensaje='Los alumnos dejan de poder responder y se entregan los exámenes.'
        textoConfirmar='Sí, finalizar'
        variante='peligro'
        onConfirmar={() => {
          setModal(null);
          void sala_.finalizar();
        }}
        onCancelar={() => setModal(null)}
      />
      <ModalConfirmacion
        abierto={modal === 'otra'}
        titulo='¿Abrir otra sala?'
        mensaje='La sala actual sigue vigente. Si abrís otra, vas a dejar de conducir esta desde el panel.'
        textoConfirmar='Sí, abrir otra'
        variante='peligro'
        onConfirmar={() => {
          setModal(null);
          onAbrirOtra();
        }}
        onCancelar={() => setModal(null)}
      />
    </>
  );
}

type Props = {
  /** Solo para pruebas: permite inyectar una conexión falsa */
  conexion?: ConexionSala;
};

export function PaginaSalaDocente({ conexion }: Props) {
  const { evaluacionId = '' } = useParams<{ evaluacionId: string }>();
  const { usuario } = useSesion();
  const usuarioId = usuario?.id ?? '';
  const [sala, setSala] = useState<SalaCreada | null>(() => recuperarSala(evaluacionId, usuarioId));
  const abrir = useAbrirSala();

  const alAbrir = (capacidadMaxima: number, duracionMinutos: number) =>
    abrir.mutate(
      { evaluacionId, capacidadMaxima, duracionMinutos },
      {
        onSuccess: (creada) => {
          guardarSala(evaluacionId, usuarioId, creada);
          setSala(creada);
        },
      },
    );

  const abrirOtra = () => {
    olvidarSala(evaluacionId);
    abrir.reset();
    setSala(null);
  };

  return (
    <main className='mx-auto flex max-w-2xl flex-col gap-6 p-6'>
      <header className='flex flex-col gap-2'>
        <Link
          to={`/evaluaciones/${evaluacionId}`}
          className='inline-flex items-center gap-1 text-sm font-bold text-tiza-bosque underline'
        >
          <ArrowLeft className='h-4 w-4' aria-hidden='true' />
          Volver a la evaluación
        </Link>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Sala en vivo</h1>
      </header>

      {sala ? (
        <PanelSala key={sala.id} sala={sala} conexion={conexion} onAbrirOtra={abrirOtra} />
      ) : (
        <FormularioAbrirSala
          cargando={abrir.isPending}
          error={abrir.isError ? mensajeAbrirSala(abrir.error) : null}
          onAbrir={alAbrir}
        />
      )}
    </main>
  );
}