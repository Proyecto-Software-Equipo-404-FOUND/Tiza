import { useCallback, useEffect, useRef } from 'react';
import type { ConexionSala } from '@/shared/realtime/tipos';

type Opciones = {
  /** Solo se registra mientras hay una pregunta en curso */
  activo: boolean;
  preguntaId: string | null;
  /** Si no hay conexión, los eventos esperan y se mandan al volver */
  puedeEnviar: boolean;
};

type EventoFoco = { duracionMs: number; tipoEvento: string; preguntaId?: string };

// Con estos errores reintentar no sirve: el evento se descarta
const DESCARTAN = ['SALA_FINALIZADA', 'ALUMNO_NO_EN_SALA', 'SALA_NO_ENCONTRADA', 'SALA_OTRA_INSTITUCION', 'NO_AUTORIZADO', 'PAYLOAD_INVALIDO'];

// El servidor guarda la duración como entero de 32 bits: se acota a un día
const MAXIMO_MS = 24 * 60 * 60 * 1000;
const MAXIMO_PENDIENTES = 20;

/**
 * Mide cuánto tiempo el alumno estuvo fuera de la pantalla (cambio de pestaña, app o ventana)
 * y lo envía al hub con `RegistrarEventoFoco`. Es un aviso para el docente: nunca bloquea nada
 * ni toca la nota (RN-CMP-07), por eso los errores no se muestran.
 */
export function useIntegridadFoco(conexion: ConexionSala, { activo, preguntaId, puedeEnviar }: Opciones) {
  const salida = useRef<{ inicio: number; tipo: string } | null>(null);
  const pendientes = useRef<EventoFoco[]>([]);
  const enVuelo = useRef(false);
  const pregunta = useRef(preguntaId);
  pregunta.current = preguntaId;
  const conectado = useRef(puedeEnviar);
  conectado.current = puedeEnviar;

  const vaciar = useCallback(async () => {
    if (enVuelo.current) return;
    enVuelo.current = true;
    try {
      while (pendientes.current.length > 0 && conectado.current) {
        const evento = pendientes.current[0];
        try {
          await conexion.invocar('RegistrarEventoFoco', evento);
        } catch (falla) {
          const texto = falla instanceof Error ? falla.message : String(falla);
          if (!DESCARTAN.some((codigo) => texto.includes(codigo))) break; // se reintenta más tarde
        }
        pendientes.current.shift();
      }
    } finally {
      enVuelo.current = false;
    }
  }, [conexion]);

  useEffect(() => {
    if (!activo) {
      // Si la sala terminó mientras estaba afuera, ese tramo no se registra
      salida.current = null;
      return;
    }

    const salir = (tipo: string) => {
      if (!salida.current) salida.current = { inicio: Date.now(), tipo };
    };
    const volver = () => {
      const actual = salida.current;
      if (!actual || document.visibilityState === 'hidden') return;
      salida.current = null;
      const evento: EventoFoco = {
        duracionMs: Math.min(MAXIMO_MS, Math.max(0, Math.round(Date.now() - actual.inicio))),
        tipoEvento: actual.tipo,
        ...(pregunta.current ? { preguntaId: pregunta.current } : {}),
      };
      pendientes.current = [...pendientes.current, evento].slice(-MAXIMO_PENDIENTES);
      void vaciar();
    };

    const alCambiarVisibilidad = () => (document.visibilityState === 'hidden' ? salir('visibilitychange') : volver());
    const alPerderFoco = () => salir('blur');

    document.addEventListener('visibilitychange', alCambiarVisibilidad);
    window.addEventListener('blur', alPerderFoco);
    window.addEventListener('focus', volver);
    return () => {
      document.removeEventListener('visibilitychange', alCambiarVisibilidad);
      window.removeEventListener('blur', alPerderFoco);
      window.removeEventListener('focus', volver);
    };
  }, [activo, vaciar]);

  // Al recuperar la conexión se mandan los eventos que quedaron esperando
  useEffect(() => {
    if (puedeEnviar && pendientes.current.length > 0) void vaciar();
  }, [puedeEnviar, vaciar]);
}
