import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';

const URL_BASE = import.meta.env.VITE_API_URL ?? '';

// Recibe una función y no el token: SignalR la vuelve a llamar en cada (re)conexión,
// así que siempre usa el accessToken vigente aunque se haya renovado.
export function crearConexion(obtenerToken: () => string | null): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${URL_BASE}/hubs/sala`, { accessTokenFactory: () => obtenerToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}
