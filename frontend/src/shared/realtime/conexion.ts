import { HubConnectionBuilder, LogLevel, type HubConnection, type IRetryPolicy } from '@microsoft/signalr';

const URL_BASE = import.meta.env.VITE_API_URL ?? '';

// Esperas entre intentos de reconexión (ms). Después del último valor se repite.
const ESPERAS_MS = [0, 2000, 5000, 10000, 15000, 30000];
// Pasado este tiempo sin conseguirlo se da por perdida la conexión y se ofrece «Reintentar»
export const LIMITE_RECONEXION_MS = 3 * 60 * 1000;

/**
 * Política de reconexión para una evaluación: insiste durante unos minutos (el valor por
 * defecto de SignalR se rinde a los ~40 segundos, poco para un corte de wifi en pleno examen).
 */
export const politicaReintentos: IRetryPolicy = {
  nextRetryDelayInMilliseconds: ({ previousRetryCount, elapsedMilliseconds }) =>
    elapsedMilliseconds >= LIMITE_RECONEXION_MS ? null : ESPERAS_MS[Math.min(previousRetryCount, ESPERAS_MS.length - 1)],
};

// Recibe una función y no el token: SignalR la vuelve a llamar en cada (re)conexión,
// así que siempre usa el accessToken vigente aunque se haya renovado.
export function crearConexion(obtenerToken: () => string | null | Promise<string | null>): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${URL_BASE}/hubs/sala`, { accessTokenFactory: async () => (await obtenerToken()) ?? '' })
    .withAutomaticReconnect(politicaReintentos)
    // Con WebSockets el token viaja en la URL y en Information la librería puede mostrarla en consola
    .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
    .build();
}
