import { useCallback, useEffect, useRef, useState } from 'react';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { codigoDeError, mensajeAccionSala } from '../errores';
import { calcularDesfaseMs, registrarAlerta, registrarIngreso } from '../panel';
import type {
  AlertaFocoEvento,
  AlumnoIngresoEvento,
  AlumnoPanel,
  EstadoSalaEvento,
  FaseSalaDocente,
  PreguntaActual,
  RespuestaRegistradaEvento,
  SalaFinalizadaEvento,
  SalaIniciadaEvento,
} from '../tipos';

type Lista = { alumnos: AlumnoPanel[]; idsVistos: ReadonlySet<string> };

/**
 * Conduce la sala desde el panel. `conexion` tiene que ser estable y `salaId` es el de una sala ya creada:
 * los eventos se registran una sola vez por conexión (`escuchar` no tiene cómo dejar de escuchar).
 */
export function useSalaDocente(conexion: ConexionSala, salaId: string) {
  const [fase, setFase] = useState<FaseSalaDocente>('espera');
  const [lista, setLista] = useState<Lista>({ alumnos: [], idsVistos: new Set() });
  const [totalConectados, setTotalConectados] = useState(0);
  const [pregunta, setPregunta] = useState<PreguntaActual | null>(null);
  // Alumnos que ya respondieron, por pregunta (llega con el evento respuestaRegistrada)
  const [respondieron, setRespondieron] = useState<Record<string, ReadonlySet<string>>>({});
  const [fechaLimiteSala, setFechaLimiteSala] = useState<string | null>(null);
  const [desfaseMs, setDesfaseMs] = useState(0);
  const [motivoCierre, setMotivoCierre] = useState<SalaFinalizadaEvento['motivo'] | null>(null);
  const [sincronizando, setSincronizando] = useState(false);
  const [trabajando, setTrabajando] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Se vuelve a unir en cada «conectado», sin depender de que el hook se vuelva a montar
  const unirseRef = useRef<() => Promise<void>>(async () => {});

  useEffect(() => {
    let activo = true;

    conexion.escuchar('alumnoIngreso', (carga) => {
      if (!activo) return;
      const evento = carga as AlumnoIngresoEvento;
      setLista((previa) => ({ ...previa, alumnos: registrarIngreso(previa.alumnos, evento) }));
      // El total lo calcula el servidor: manda sobre el conteo local
      setTotalConectados(evento.totalConectados);
    });
    conexion.escuchar('alertaFoco', (carga) => {
      if (!activo) return;
      const evento = carga as AlertaFocoEvento;
      setLista((previa) => registrarAlerta(previa.alumnos, evento, previa.idsVistos));
    });
    conexion.escuchar('estadoSala', (carga) => {
      if (!activo) return;
      const estado = carga as EstadoSalaEvento;
      setPregunta(estado.preguntaActual ?? null);
      setFechaLimiteSala(estado.fechaLimiteUtc ?? null);
      if (estado.servidorUtc) setDesfaseMs(calcularDesfaseMs(estado.servidorUtc));
      setFase(estado.estado === 'EnCurso' ? 'enCurso' : estado.estado === 'Finalizada' ? 'finalizada' : 'espera');
    });
    conexion.escuchar('salaIniciada', (carga) => {
      if (!activo) return;
      setFechaLimiteSala((carga as SalaIniciadaEvento).fechaLimiteUtc);
      setFase('enCurso');
    });
    conexion.escuchar('preguntaActual', (carga) => {
      if (!activo) return;
      setPregunta(carga as PreguntaActual);
      setFase('enCurso');
    });
    conexion.escuchar('respuestaRegistrada', (carga) => {
      if (!activo) return;
      const evento = carga as RespuestaRegistradaEvento;
      setRespondieron((previo) => {
        const ids = new Set(previo[evento.preguntaId] ?? []);
        ids.add(evento.alumnoId);
        return { ...previo, [evento.preguntaId]: ids };
      });
    });
    // Foto de presencia que manda el hub al unirse el docente: solo se usa el total
    conexion.escuchar('alumnosConectados', (carga) => {
      if (!activo) return;
      const total = (carga as { totalConectados?: unknown }).totalConectados;
      if (typeof total === 'number') setTotalConectados(total);
    });
    conexion.escuchar('salaFinalizada', (carga) => {
      if (!activo) return;
      setMotivoCierre((carga as SalaFinalizadaEvento).motivo);
      setFase('finalizada');
    });

    const dejarDeEscucharEstado = conexion.alCambiarEstado((estado) => {
      if (activo && estado === 'conectado') void unirseRef.current();
    });

    return () => {
      activo = false;
      dejarDeEscucharEstado();
    };
  }, [conexion]);

  // Tras unirse, el hub no manda el estado: se pide aparte (la reconexión pierde la sala y los grupos)
  const unirse = useCallback(async () => {
    setSincronizando(true);
    setError(null);
    try {
      await conexion.invocar('UnirseASalaDocente', salaId);
      await conexion.invocar('ObtenerEstadoSala');
    } catch (falla) {
      setError(mensajeAccionSala(falla));
    } finally {
      setSincronizando(false);
    }
  }, [conexion, salaId]);
  unirseRef.current = unirse;

  const ejecutar = useCallback(
    async (metodo: string, ...argumentos: unknown[]) => {
      if (trabajando) return;
      setTrabajando(true);
      setError(null);
      try {
        await conexion.invocar(metodo, ...argumentos);
      } catch (falla) {
        setError(mensajeAccionSala(falla));
        // Si ya estaba iniciada, el panel solo estaba desactualizado: se pide el estado
        if (codigoDeError(falla) === 'SALA_YA_INICIADA') await conexion.invocar('ObtenerEstadoSala').catch(() => {});
      } finally {
        setTrabajando(false);
      }
    },
    [conexion, trabajando],
  );

  const iniciar = useCallback(() => ejecutar('IniciarSala'), [ejecutar]);
  // SignalR no admite parámetros opcionales: el hub declara AvanzarPregunta(Guid? preguntaId),
  // así que hay que mandar el argumento. null = avanzar a la siguiente en orden.
  const siguiente = useCallback(() => ejecutar('AvanzarPregunta', null), [ejecutar]);
  const finalizar = useCallback(() => ejecutar('FinalizarSala', 'Docente'), [ejecutar]);

  const puedeAvanzar = fase === 'enCurso' && !!pregunta && pregunta.indice < pregunta.totalPreguntas;

  return {
    fase,
    alumnos: lista.alumnos,
    totalConectados,
    pregunta,
    fechaLimiteSala,
    desfaseMs,
    motivoCierre,
    sincronizando,
    trabajando,
    error,
    puedeAvanzar,
    respondieronPreguntaActual: pregunta ? (respondieron[pregunta.preguntaId]?.size ?? 0) : 0,
    reunirse: unirse,
    iniciar,
    siguiente,
    finalizar,
  };
}