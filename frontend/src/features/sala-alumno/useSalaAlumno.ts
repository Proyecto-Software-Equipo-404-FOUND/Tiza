import { useCallback, useEffect, useRef, useState } from 'react';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { codigoDeError, mensajeDeCodigo } from './errores';
import { enviarConReintentos, esperarReal, nuevaClave } from './envio';
import type {
  EnvioPendiente,
  ErrorAsincronoEvento,
  EstadoSalaEvento,
  FaseSala,
  PreguntaActual,
  ResultadoEnvio,
  SalaFinalizadaEvento,
} from './tipos';

const MENSAJES_RECHAZO: Record<string, string> = {
  PREGUNTA_NO_VIGENTE: 'La pregunta cambió. Mirá la pregunta actual.',
  TIEMPO_AGOTADO: 'Se terminó el tiempo para responder.',
  SALA_FINALIZADA: 'La sala ya finalizó.',
  SALA_NO_INICIADA: 'La sala todavía no empezó.',
  RESPUESTA_INVALIDA: 'Revisá tu respuesta e intentá de nuevo.',
  PAYLOAD_INVALIDO: 'Revisá tu respuesta e intentá de nuevo.',
  SECUENCIA_FUERA_DE_ORDEN: 'Sincronizamos tu sala. Enviá tu respuesta de nuevo.',
  IDEMPOTENCY_KEY_REUTILIZADA: 'No pudimos enviar tu respuesta. Probá de nuevo.',
  SESION_REEMPLAZADA: 'Abriste la sala en otra pestaña o dispositivo. Seguí desde ahí o recargá esta página.',
  ALUMNO_NO_EN_SALA: 'No encontramos tu sala. Recargá la página e ingresá el PIN de nuevo.',
  SALA_NO_ENCONTRADA: 'La sala ya no está disponible.',
  SALA_OTRA_INSTITUCION: 'No podés responder en esta sala.',
};

// Con estos rechazos reintentar no ayuda: se deja de ofrecer el envío hasta que llegue otra pregunta
const CIERRAN_EL_ENVIO = new Set([
  'TIEMPO_AGOTADO',
  'SALA_FINALIZADA',
  'SESION_REEMPLAZADA',
  'ALUMNO_NO_EN_SALA',
  'SALA_NO_ENCONTRADA',
  'SALA_OTRA_INSTITUCION',
]);

const MENSAJE_RED = 'No pudimos enviar tu respuesta. Revisá tu conexión y probá de nuevo.';

/**
 * Lleva al alumno desde el ingreso con PIN hasta el final de la sala: espera, preguntas y respuestas.
 * Los datos de la sala llegan por eventos del hub; `UnirseASala` solo confirma el ingreso.
 */
export function useSalaAlumno(conexion: ConexionSala, esperar: (ms: number) => Promise<void> = esperarReal) {
  const [fase, setFase] = useState<FaseSala>('ingreso');
  const [error, setError] = useState<string | null>(null);
  const [motivoCierre, setMotivoCierre] = useState<SalaFinalizadaEvento['motivo'] | null>(null);
  const [pregunta, setPregunta] = useState<PreguntaActual | null>(null);
  const [respondidas, setRespondidas] = useState<string[]>([]);
  const [desfaseMs, setDesfaseMs] = useState(0);
  const [enviando, setEnviando] = useState(false);
  const [errorEnvio, setErrorEnvio] = useState<string | null>(null);
  const [envioCerrado, setEnvioCerrado] = useState(false);

  // Contador por sala (no por pregunta): la primera respuesta usa 1 y cada aceptada suma 1
  const ultimaSecuencia = useRef(0);
  // Envío cuyo resultado se desconoce: un reintento usa la misma clave y secuencia
  const pendiente = useRef<EnvioPendiente | null>(null);

  useEffect(() => {
    // `escuchar` no devuelve cómo dejar de escuchar: al desmontar se ignoran los eventos
    let activo = true;

    conexion.escuchar('estadoSala', (carga) => {
      if (!activo) return;
      const estado = carga as EstadoSalaEvento;
      ultimaSecuencia.current = estado.ultimaSecuencia ?? ultimaSecuencia.current;
      setRespondidas((previas) => [...new Set([...previas, ...(estado.preguntasRespondidas ?? [])])]);
      setPregunta(estado.preguntaActual ?? null);
      // El reloj del servidor es la referencia: se guarda la diferencia con el del navegador
      if (estado.servidorUtc) setDesfaseMs(new Date(estado.servidorUtc).getTime() - Date.now());

      if (estado.estado === 'EnCurso') setFase('enCurso');
      else if (estado.estado === 'Finalizada') setFase('finalizada');
      else setFase('espera');
    });
    conexion.escuchar('salaIniciada', () => {
      if (activo) setFase('enCurso');
    });
    conexion.escuchar('preguntaActual', (carga) => {
      if (!activo) return;
      setPregunta(carga as PreguntaActual);
      setErrorEnvio(null);
      setEnvioCerrado(false);
      pendiente.current = null;
      setFase('enCurso');
    });
    conexion.escuchar('salaFinalizada', (carga) => {
      if (!activo) return;
      setMotivoCierre((carga as SalaFinalizadaEvento).motivo);
      setFase('finalizada');
    });
    conexion.escuchar('error', (carga) => {
      if (activo) setError((carga as ErrorAsincronoEvento).mensaje);
    });

    return () => {
      activo = false;
    };
  }, [conexion]);

  const unirse = useCallback(
    async (pin: number) => {
      setError(null);
      setFase('uniendo');
      try {
        await conexion.invocar('UnirseASala', pin);
        // El evento `estadoSala` suele llegar antes y ya movió la fase: no se pisa
        setFase((actual) => (actual === 'uniendo' ? 'espera' : actual));
      } catch (falla) {
        const codigo = codigoDeError(falla);
        if (codigo === 'ALUMNO_YA_EN_SALA') {
          // Reconexión: el contrato pide tratarlo como éxito y recuperar el estado
          setFase((actual) => (actual === 'uniendo' ? 'espera' : actual));
          await conexion.invocar('ObtenerEstadoSala').catch(() => {
            // todavía no está implementado en el servidor (S2-SA-02)
          });
          return;
        }
        setError(mensajeDeCodigo(codigo));
        setFase('ingreso');
      }
    },
    [conexion],
  );

  const sincronizar = useCallback(
    () =>
      conexion.invocar('ObtenerEstadoSala').catch(() => {
        // todavía no está implementado en el servidor (S2-SA-02)
      }),
    [conexion],
  );

  const resolver = useCallback(
    async (envio: EnvioPendiente, resultado: ResultadoEnvio, puedeReenviar: boolean): Promise<'reenviar' | void> => {
      const marcarRespondida = () =>
        setRespondidas((previas) => (previas.includes(envio.preguntaId) ? previas : [...previas, envio.preguntaId]));

      if (resultado.estado === 'Aceptada' || resultado.estado === 'Duplicada') {
        // Duplicada también confirma que la secuencia quedó consumida
        ultimaSecuencia.current = envio.secuencia;
        pendiente.current = null;
        marcarRespondida();
        return;
      }

      const codigo = resultado.error?.codigo ?? 'ERROR_INTERNO';
      if (codigo === 'RESPUESTA_YA_REGISTRADA') {
        // La primera respuesta es la definitiva: no se reintenta con otra clave
        pendiente.current = null;
        marcarRespondida();
        return;
      }

      if (codigo === 'SECUENCIA_FUERA_DE_ORDEN') {
        // Repetir el envío no sirve: se pide el estado (trae la última secuencia) y se reenvía UNA vez
        // la misma respuesta con la misma clave y la secuencia que corresponde
        await sincronizar();
        const enEspera = pendiente.current;
        if (puedeReenviar && enEspera && enEspera.preguntaId === envio.preguntaId) {
          pendiente.current = { ...enEspera, secuencia: ultimaSecuencia.current + 1 };
          return 'reenviar';
        }
        pendiente.current = null;
        setErrorEnvio(MENSAJES_RECHAZO[codigo]);
        return;
      }

      // Un rechazo no consume secuencia ni guarda la clave
      if (codigo !== 'ERROR_INTERNO') pendiente.current = null;
      if (CIERRAN_EL_ENVIO.has(codigo)) setEnvioCerrado(true);
      setErrorEnvio(MENSAJES_RECHAZO[codigo] ?? resultado.error?.mensaje ?? MENSAJE_RED);
      if (codigo === 'PREGUNTA_NO_VIGENTE') await sincronizar();
    },
    [sincronizar],
  );

  const enviarRespuesta = useCallback(
    async (preguntaId: string, respuesta: string) => {
      const texto = respuesta.trim();
      setErrorEnvio(null);
      setEnviando(true);

      // Reintento manual del mismo contenido: se conserva la clave y la secuencia
      const previo = pendiente.current;
      const envio: EnvioPendiente =
        previo && previo.preguntaId === preguntaId && previo.respuesta === texto
          ? previo
          : {
              preguntaId,
              respuesta: texto,
              idempotencyKey: nuevaClave(),
              secuencia: ultimaSecuencia.current + 1,
            };
      pendiente.current = envio;

      try {
        let actual = envio;
        for (let vuelta = 0; ; vuelta++) {
          const accion = await resolver(actual, await enviarConReintentos(conexion, actual, esperar), vuelta === 0);
          if (accion !== 'reenviar' || !pendiente.current) break;
          actual = pendiente.current;
        }
      } catch {
        // Sin acuse: el envío queda pendiente para reintentarlo con la misma clave
        setErrorEnvio(MENSAJE_RED);
      } finally {
        setEnviando(false);
      }
    },
    [conexion, esperar, resolver],
  );

  return {
    fase,
    error,
    motivoCierre,
    pregunta,
    respondidas,
    desfaseMs,
    enviando,
    errorEnvio,
    envioCerrado,
    unirse,
    enviarRespuesta,
  };
}
