import { useCallback, useEffect, useState } from 'react';
import type { ConexionSala, EstadoConexion } from './tipos';

export function useConexionSala(conexion: ConexionSala) {
  const [estado, setEstado] = useState<EstadoConexion>('conectando');
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    let activo = true;
    conexion.alCambiarEstado((nuevo) => {
      if (activo) setEstado(nuevo);
    });
    conexion.iniciar().catch(() => {
      // el estado 'desconectado' ya lo informa la conexión
    });
    return () => {
      activo = false;
      void conexion.detener();
    };
  }, [conexion, intento]);

  const reintentar = useCallback(() => setIntento((n) => n + 1), []);

  return { estado, reintentar };
}
