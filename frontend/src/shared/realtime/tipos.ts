export type Manejador = (...args: unknown[]) => void;

export type EstadoConexion = 'conectando' | 'conectado' | 'reconectando' | 'desconectado';

export type ConexionSala = {
  iniciar: () => Promise<void>;
  detener: () => Promise<void>;
  escuchar: (evento: string, manejador: Manejador) => void;
  // Devuelve lo que responde el hub (por ejemplo, el resultado de EnviarRespuesta)
  invocar: (metodo: string, ...args: unknown[]) => Promise<unknown>;
  // Devuelve una función para dejar de escuchar (hay que llamarla al limpiar el efecto)
  alCambiarEstado: (manejador: (estado: EstadoConexion) => void) => () => void;
};
