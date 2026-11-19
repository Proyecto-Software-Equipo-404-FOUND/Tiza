import { Check, X } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useSesion } from '@/features/auth/SesionContext';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { ModalConfirmacion } from '@/shared/components/ModalConfirmacion';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { CampoPuntajeFinal } from '../components/CampoPuntajeFinal';
import {
  AlertasFoco,
  EtiquetaSugerida,
  InsigniaEstadoEntrega,
  InsigniaGravedad,
} from '../components/InsigniasResultados';
import { TarjetaNotaFinal } from '../components/TarjetaNotaFinal';
import {
  alertasDePregunta,
  esRoja,
  etiquetaPreguntaDeAlerta,
  formatearDuracion,
  formatearFechaHora,
  formatearNota,
  formatearNumero,
  resumenSalidas,
} from '../formato';
import { useAprobarNota } from '../hooks/useAprobarNota';
import { useEntrega } from '../hooks/useEntregas';
import { useNombresAlumnos } from '../hooks/useNombresAlumnos';
import { mensajeNotaFinal } from '../errores';
import { armarCambios, esEditable, leerPuntaje, puntajesIniciales, resumirNota } from '../notaFinal';
import type { AlertaFocoDetalle, DesgloseCriterio, EntregaDetalle, RespuestaEntrega } from '../tipos';

const columnasDesglose: Columna<DesgloseCriterio>[] = [
  { titulo: 'Criterio', celda: (fila) => fila.criterio ?? '—' },
  { titulo: 'Puntaje', celda: (fila) => (fila.puntaje == null ? '—' : formatearNumero(fila.puntaje)) },
  { titulo: 'Comentario', celda: (fila) => fila.comentario ?? '' },
];

function RespuestaMultipleChoice({ respuesta }: { respuesta: RespuestaEntrega }) {
  if (!respuesta.respuesta) return <p className='text-tiza-bosque/70'>No respondió.</p>;
  return (
    <p className='flex flex-wrap items-center gap-2'>
      {respuesta.esCorrecta ? (
        <Check className='h-4 w-4 text-tiza-pasto' aria-hidden='true' />
      ) : (
        <X className='h-4 w-4 text-tiza-coral' aria-hidden='true' />
      )}
      <span>
        Eligió: <strong>{respuesta.opcionElegidaTexto ?? respuesta.respuesta}</strong>
      </span>
      <span className='text-sm font-semibold text-tiza-bosque/80'>
        {respuesta.esCorrecta ? '(correcta)' : '(incorrecta)'}
        {respuesta.puntajeObtenido != null && ` · ${formatearNota(respuesta.puntajeObtenido, respuesta.puntajeMaximo)}`}
      </span>
    </p>
  );
}

function RespuestaAbierta({ respuesta }: { respuesta: RespuestaEntrega }) {
  const desglose = respuesta.desglosePorCriterio ?? [];
  return (
    <div className='flex flex-col gap-3'>
      {respuesta.respuesta ? (
        <p className='whitespace-pre-wrap rounded-xl bg-tiza-fondo p-3'>{respuesta.respuesta}</p>
      ) : (
        <p className='text-tiza-bosque/70'>No respondió.</p>
      )}

      {respuesta.estadoCorreccion === 'PendienteIA' && (
        <p className='text-sm text-tiza-bosque/80'>La IA todavía no corrigió esta respuesta.</p>
      )}

      {respuesta.notaSugerida != null && (
        <div className='flex flex-col gap-2 rounded-xl border border-tiza-violeta/30 bg-tiza-violeta/5 p-3'>
          <p className='flex flex-wrap items-center gap-2 font-semibold text-tiza-bosque'>
            Nota de la IA: {formatearNota(respuesta.notaSugerida, respuesta.puntajeMaximo)}
            <EtiquetaSugerida />
          </p>
          {respuesta.justificacionIA && (
            <p className='text-sm text-tiza-bosque/80'>
              <span className='font-bold'>Justificación de la IA: </span>
              {respuesta.justificacionIA}
            </p>
          )}
          {desglose.length > 0 && (
            <Tabla columnas={columnasDesglose} filas={desglose} clave={(fila) => fila.criterio ?? String(fila.puntaje)} />
          )}
        </div>
      )}
    </div>
  );
}

function TarjetaRespuesta({
  respuesta,
  numero,
  alertas,
  campoNota,
}: {
  respuesta: RespuestaEntrega;
  numero: number;
  alertas: AlertaFocoDetalle[];
  /** Campo para que el docente confirme o corrija la nota (solo preguntas abiertas) */
  campoNota?: ReactNode;
}) {
  return (
    <li className='rounded-2xl border border-tiza-borde bg-white p-5'>
      <h3 className='mb-3 font-heading text-lg font-extrabold text-tiza-bosque'>
        {numero}. {respuesta.enunciado}
      </h3>
      {respuesta.tipo === 'OpcionMultiple' ? (
        <RespuestaMultipleChoice respuesta={respuesta} />
      ) : (
        <RespuestaAbierta respuesta={respuesta} />
      )}
      {campoNota}
      {alertas.length > 0 && (
        <p className='mt-3 flex flex-wrap items-center gap-2 text-sm text-tiza-bosque/80'>
          <InsigniaGravedad roja={alertas.some(esRoja)}>{alertas.some(esRoja) ? 'Roja' : 'Amarilla'}</InsigniaGravedad>
          {resumenSalidas(alertas)}
        </p>
      )}
    </li>
  );
}

const columnasAlertas = (preguntaIds: string[]): Columna<AlertaFocoDetalle>[] => [
  { titulo: 'Pregunta', celda: (alerta) => etiquetaPreguntaDeAlerta(alerta, preguntaIds) },
  {
    titulo: 'Gravedad',
    celda: (alerta) =>
      esRoja(alerta) ? (
        <InsigniaGravedad roja>Roja</InsigniaGravedad>
      ) : (
        <InsigniaGravedad roja={false}>Amarilla</InsigniaGravedad>
      ),
  },
  { titulo: 'Estuvo fuera', celda: (alerta) => formatearDuracion(alerta.duracionMs) },
  { titulo: 'Cuándo', celda: (alerta) => formatearFechaHora(alerta.registradoUtc) },
];

function DetalleEntrega({ entrega, evaluacionId, nombre }: { entrega: EntregaDetalle; evaluacionId: string; nombre: string }) {
  // El componente se vuelve a montar (key) solo cuando cambia de entrega. Si al aprobar falla un paso y la pantalla se
  // refresca, lo escrito y el mensaje de error siguen ahí.
  const [escritos, setEscritos] = useState(() => puntajesIniciales(entrega.respuestas));
  const [confirmando, setConfirmando] = useState(false);
  const aprobar = useAprobarNota(evaluacionId, entrega.entregaId);
  const { usuario } = useSesion();
  const aprobada = entrega.estadoNota === 'Aprobada';
  // Editar y aprobar es solo del docente dueño del examen (el administrador recibe 403 por contrato)
  const soloLectura = usuario?.rol !== 'docente';
  // Con la nota aprobada se muestra lo guardado, no lo que quedó escrito en los campos
  const valores = aprobada ? puntajesIniciales(entrega.respuestas) : escritos;
  const resumen = resumirNota(entrega.respuestas, valores);
  const hayQueEditar = entrega.respuestas.some(esEditable);

  // Un campo vacío no marca error: ya lo avisa la tarjeta de nota final
  const errorDe = (respuesta: RespuestaEntrega) => {
    const texto = valores[respuesta.preguntaId] ?? '';
    return texto.trim() === '' ? null : leerPuntaje(texto, respuesta.puntajeMaximo).error;
  };

  const confirmar = () => {
    setConfirmando(false);
    const cambios = armarCambios(entrega, valores);
    if (cambios) aprobar.mutate(cambios);
  };

  return (
    <>
      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>{nombre}</h1>
        <p className='mt-3 flex flex-wrap items-center gap-3 text-sm text-tiza-bosque/80'>
          <InsigniaEstadoEntrega estado={entrega.estado} aprobada={aprobada} sinRespuestas={entrega.respondioAlgo === false} />
          <span>Entregó el {formatearFechaHora(entrega.fechaEntregaUtc)}</span>
          {entrega.respondioAlgo === false && <span>No respondió nada</span>}
        </p>
      </header>

      <Tarjeta
        titulo='Notas'
        subtitulo={
          aprobada
            ? 'La nota de la IA fue solo una sugerencia. La nota final ya está corregida.'
            : soloLectura
              ? 'La nota de la IA es solo una sugerencia. La nota final la define el docente.'
              : 'La nota de la IA es solo una sugerencia. La nota final es la que aprobás más abajo.'
        }
      >
        <dl className='grid gap-4 sm:grid-cols-3'>
          <div>
            <dt className='text-sm font-bold text-tiza-bosque/70'>Múltiple choice</dt>
            <dd>{formatearNota(entrega.notaMultipleChoice, entrega.puntajeMaximoMultipleChoice) ?? '—'}</dd>
          </div>
          <div>
            <dt className='text-sm font-bold text-tiza-bosque/70'>Abiertas (IA)</dt>
            <dd className='flex flex-wrap items-center gap-2'>
              {entrega.puntajeMaximoAbiertas == null
                ? '—'
                : entrega.respondioAlgo === false
                  ? 'Sin respuestas'
                  : (formatearNota(entrega.notaSugeridaAbiertas, entrega.puntajeMaximoAbiertas) ?? 'Pendiente')}
              {entrega.respondioAlgo !== false && entrega.notaSugeridaAbiertas != null && <EtiquetaSugerida />}
            </dd>
          </div>
          <div>
            <dt className='text-sm font-bold text-tiza-bosque/70'>Alertas de foco</dt>
            <dd>
              <AlertasFoco alertas={entrega.alertasFoco} />
            </dd>
          </div>
        </dl>
      </Tarjeta>

      <TarjetaNotaFinal
        resumen={resumen}
        aprobada={aprobada}
        soloLectura={soloLectura}
        notaAprobada={entrega.notaFinal}
        hayQueEditar={hayQueEditar}
        cargando={aprobar.isPending}
        error={aprobar.isError ? mensajeNotaFinal(aprobar.error) : null}
        onAprobar={() => setConfirmando(true)}
      />

      <section aria-labelledby='titulo-respuestas' className='flex flex-col gap-3'>
        <h2 id='titulo-respuestas' className='font-heading text-2xl font-extrabold text-tiza-bosque'>
          Respuestas
        </h2>
        {entrega.respuestas.length === 0 ? (
          <p className='text-tiza-bosque/70'>No hay respuestas para mostrar.</p>
        ) : (
          <ol className='flex flex-col gap-4'>
            {entrega.respuestas.map((respuesta, indice) => (
              <TarjetaRespuesta
                key={respuesta.preguntaId}
                respuesta={respuesta}
                numero={indice + 1}
                alertas={alertasDePregunta(entrega.alertas, respuesta.preguntaId)}
                campoNota={
                  esEditable(respuesta) ? (
                    <CampoPuntajeFinal
                      numero={indice + 1}
                      maximo={respuesta.puntajeMaximo}
                      sugerida={respuesta.notaSugerida}
                      valor={valores[respuesta.preguntaId] ?? ''}
                      error={errorDe(respuesta)}
                      deshabilitado={aprobada || soloLectura}
                      onCambiar={(texto) => setEscritos((previos) => ({ ...previos, [respuesta.preguntaId]: texto }))}
                    />
                  ) : undefined
                }
              />
            ))}
          </ol>
        )}
      </section>

      {entrega.alertas.length > 0 && (
        <Tarjeta
          titulo='Salidas de la pantalla'
          subtitulo='Es un aviso: no modifica la nota. Amarilla: menos de 3 s. Roja: 3 s o más.'
        >
          <Tabla columnas={columnasAlertas(entrega.respuestas.map((r) => r.preguntaId))} filas={entrega.alertas} clave={(alerta) => `${alerta.registradoUtc}-${alerta.tipoEvento}-${alerta.duracionMs}`} />
        </Tarjeta>
      )}

      <ModalConfirmacion
        abierto={confirmando}
        titulo='¿Aprobar la nota final?'
        mensaje={`La nota final va a ser ${formatearNota(resumen.total, resumen.maximo) ?? ''}. Después de aprobarla, el alumno la puede ver.`}
        textoConfirmar='Sí, aprobar'
        variante='principal'
        onConfirmar={confirmar}
        onCancelar={() => setConfirmando(false)}
      />
    </>
  );
}

export function PaginaEntrega() {
  const { evaluacionId = '', entregaId = '' } = useParams<{ evaluacionId: string; entregaId: string }>();
  const { data: entrega, isPending, isError, error, refetch } = useEntrega(evaluacionId, entregaId);
  const { nombreDe } = useNombresAlumnos();

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <Link
        to={`/evaluaciones/${evaluacionId}/resultados`}
        className='text-sm font-semibold text-tiza-pasto hover:underline'
      >
        ← Volver a resultados
      </Link>

      {isPending && <Spinner texto='Cargando la entrega…' />}
      {isError && <BannerError mensaje={mensajeDeError(error)} onReintentar={() => refetch()} />}

      {entrega && (
        <DetalleEntrega
          key={entrega.entregaId}
          entrega={entrega}
          evaluacionId={evaluacionId}
          nombre={nombreDe(entrega.alumnoId)}
        />
      )}
    </main>
  );
}
