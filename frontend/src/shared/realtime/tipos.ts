export type Manejador = (...args: unknown[]) => void;

export type EstadoConexion = 'conectando' | 'conectado' | 'reconectando' | 'desconectado';

export type ConexionSala = {
  iniciar: () => Promise<void>;
  detener: () => Promise<void>;
  escuchar: (evento: string, manejador: Manejador) => void;
  invocar: (metodo: string, ...args: unknown[]) => Promise<void>;
  alCambiarEstado: (manejador: (estado: EstadoConexion) => void) => void;
};
