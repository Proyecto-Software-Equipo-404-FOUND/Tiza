import type { ConexionSala, EstadoConexion, Manejador } from './tipos';

export function crearConexionFalsa() {
  const manejadores = new Map<string, Manejador[]>();
  const oyentes: Array<(estado: EstadoConexion) => void> = [];

  const simularEstado = (estado: EstadoConexion) => oyentes.forEach((oyente) => oyente(estado));

  const conexion: ConexionSala = {
    iniciar: async () => {
      simularEstado('conectado');
    },
    detener: async () => {
      simularEstado('desconectado');
    },
    escuchar: (evento, manejador) => {
      manejadores.set(evento, [...(manejadores.get(evento) ?? []), manejador]);
    },
    invocar: async () => {},
    alCambiarEstado: (manejador) => {
      oyentes.push(manejador);
    },
  };

  // Solo para pruebas: simula un mensaje que llega desde el servidor
  const simular = (evento: string, ...args: unknown[]) => {
    manejadores.get(evento)?.forEach((manejador) => manejador(...args));
  };

  return { conexion, simular, simularEstado };
}
