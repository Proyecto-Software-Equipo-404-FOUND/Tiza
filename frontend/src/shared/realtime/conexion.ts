import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';

const URL_BASE = import.meta.env.VITE_API_URL ?? '';

export function crearConexion(token: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${URL_BASE}/hubs/sala`, { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}
