import { useCallback, useEffect, useState } from 'react';
import type { ConexionSala } from '@/shared/realtime/tipos';
import { codigoDeError, mensajeDeCodigo } from './errores';
import type {
  ErrorAsincronoEvento,
  EstadoSalaEvento,
  FaseSala,
  SalaFinalizadaEvento,
} from './tipos';

/**
 * Lleva al alumno desde el ingreso con PIN hasta que la sala empieza o termina.
 * Los datos de la sala llegan por eventos del hub; `UnirseASala` solo confirma el ingreso.
 */
export function useSalaAlumno(conexion: ConexionSala) {
  const [fase, setFase] = useState<FaseSala>('ingreso');
  const [error, setError] = useState<string | null>(null);
  const [motivoCierre, setMotivoCierre] = useState<SalaFinalizadaEvento['motivo'] | null>(null);

  useEffect(() => {
    // `escuchar` no devuelve cómo dejar de escuchar: al desmontar se ignoran los eventos
    let activo = true;

    conexion.escuchar('estadoSala', (carga) => {
      if (!activo) return;
      const { estado } = carga as EstadoSalaEvento;
      if (estado === 'EnCurso') setFase('enCurso');
      else if (estado === 'Finalizada') setFase('finalizada');
      else setFase('espera');
    });
    conexion.escuchar('salaIniciada', () => {
      if (activo) setFase('enCurso');
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

  return { fase, error, motivoCierre, unirse };
}
