import { useCallback, useEffect, useRef, useState } from 'react';
import type { ConexionSala, EstadoConexion } from './tipos';

/**
 * `conexion` tiene que ser estable: crearla fuera del componente o con useMemo/useRef.
 * Si se crea de nuevo en cada render, el efecto se reinicia sin parar.
 */
export function useConexionSala(conexion: ConexionSala) {
  const [estado, setEstado] = useState<EstadoConexion>('conectando');
  const [intento, setIntento] = useState(0);
  // En desarrollo, React.StrictMode monta el efecto, lo limpia y lo vuelve a montar.
  // Guardamos la última detención para no llamar a iniciar() antes de que termine.
  const detencion = useRef<Promise<void>>(Promise.resolve());

  useEffect(() => {
    let activo = true;
    const dejarDeEscuchar = conexion.alCambiarEstado((nuevo) => {
      if (activo) setEstado(nuevo);
    });

    const arranque = detencion.current
      .then(() => conexion.iniciar())
      .catch(() => {
        // el estado 'desconectado' ya lo informa la conexión
      });

    return () => {
      activo = false;
      dejarDeEscuchar();
      detencion.current = arranque.then(() => conexion.detener()).catch(() => {});
    };
  }, [conexion, intento]);

  const reintentar = useCallback(() => setIntento((n) => n + 1), []);

  return { estado, reintentar };
}
