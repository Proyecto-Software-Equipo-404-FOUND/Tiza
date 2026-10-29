import type { HubConnection } from '@microsoft/signalr';
import type { ConexionSala, EstadoConexion } from './tipos';

export function conexionReal(hub: HubConnection): ConexionSala {
  const oyentes = new Set<(estado: EstadoConexion) => void>();
  const avisar = (estado: EstadoConexion) => oyentes.forEach((oyente) => oyente(estado));

  hub.onreconnecting(() => avisar('reconectando'));
  hub.onreconnected(() => avisar('conectado'));
  hub.onclose(() => avisar('desconectado'));

  return {
    iniciar: async () => {
      avisar('conectando');
      try {
        await hub.start();
        avisar('conectado');
      } catch (error) {
        avisar('desconectado');
        throw error;
      }
    },
    detener: () => hub.stop(),
    escuchar: (evento, manejador) => hub.on(evento, manejador),
    invocar: (metodo, ...args) => hub.invoke(metodo, ...args),
    alCambiarEstado: (manejador) => {
      oyentes.add(manejador);
      return () => {
        oyentes.delete(manejador);
      };
    },
  };
}
