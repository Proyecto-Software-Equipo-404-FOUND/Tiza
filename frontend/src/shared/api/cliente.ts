import type { ProblemDetails } from './tipos';

const URL_BASE = import.meta.env.VITE_API_URL ?? '';
const RUTA_RENOVAR = '/api/v1/identidad/auth/refresh';

let token: string | null = sessionStorage.getItem('token');
let tokenRenovacion: string | null = sessionStorage.getItem('refreshToken');

// Al guardar null (cerrar sesión o sesión vencida) se limpia también el token de renovación
export function guardarToken(nuevo: string | null) {
  token = nuevo;
  if (nuevo) sessionStorage.setItem('token', nuevo);
  else {
    sessionStorage.removeItem('token');
    guardarTokenRenovacion(null);
  }
}

export function guardarTokenRenovacion(nuevo: string | null) {
  tokenRenovacion = nuevo;
  if (nuevo) sessionStorage.setItem('refreshToken', nuevo);
  else sessionStorage.removeItem('refreshToken');
}

export const obtenerToken = () => token;
export const obtenerTokenRenovacion = () => tokenRenovacion;

export class ApiError extends Error {
  estado: number;
  detalle: ProblemDetails | null;

  constructor(estado: number, detalle: ProblemDetails | null) {
    super(detalle?.title ?? `Error ${estado}`);
    this.estado = estado;
    this.detalle = detalle;
  }
}

// Pide un accessToken nuevo con el refreshToken. Devuelve true si salió bien.
async function renovar(): Promise<boolean> {
  if (!tokenRenovacion) return false;
  try {
    const respuesta = await fetch(`${URL_BASE}${RUTA_RENOVAR}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: tokenRenovacion }),
    });
    if (!respuesta.ok) return false;
    const datos = (await respuesta.json()) as { accessToken: string; refreshToken?: string };
    guardarToken(datos.accessToken);
    if (datos.refreshToken) guardarTokenRenovacion(datos.refreshToken);
    return true;
  } catch {
    return false;
  }
}

// Segundos de margen: un accessToken que vence en menos que esto se renueva antes de usarlo
const MARGEN_VENCIMIENTO_S = 60;

// Segundos que le quedan al token según su claim `exp`, o null si no se puede leer
function segundosRestantes(jwt: string): number | null {
  try {
    const carga = jwt.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const { exp } = JSON.parse(atob(carga)) as { exp?: number };
    return typeof exp === 'number' ? exp - Date.now() / 1000 : null;
  } catch {
    return null;
  }
}

// Si varios pedidos fallan con 401 a la vez, comparten una sola renovación
let renovacionEnCurso: Promise<boolean> | null = null;
function renovarUnaVez(): Promise<boolean> {
  renovacionEnCurso ??= renovar().finally(() => {
    renovacionEnCurso = null;
  });
  return renovacionEnCurso;
}

export async function pedir<T>(
  ruta: string,
  opciones: RequestInit = {},
  esReintento = false,
): Promise<T> {
  const respuesta = await fetch(`${URL_BASE}${ruta}`, {
    ...opciones,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...opciones.headers,
    },
  });

  if (!respuesta.ok) {
    // Con un token guardado, un 401 significa que el accessToken venció: se intenta renovar una vez
    if (respuesta.status === 401 && token) {
      const esRutaDeAuth = ruta.includes('/auth/');
      if (!esReintento && !esRutaDeAuth && (await renovarUnaVez())) {
        return pedir<T>(ruta, opciones, true);
      }
      guardarToken(null);
      window.location.assign('/login');
    }
    const detalle = await respuesta.json().catch(() => null);
    throw new ApiError(respuesta.status, detalle);
  }

  if (respuesta.status === 204) return undefined as T;
  return respuesta.json() as Promise<T>;
}

/**
 * accessToken para abrir (o reabrir) la conexión de la sala. SignalR lo pide en cada reconexión, y el
 * accessToken dura 15 minutos: si ya venció o está por vencer se renueva antes, porque con uno vencido el
 * servidor rechazaría cada intento (los pedidos HTTP se renuevan solos recién ante un 401; el hub no).
 * Si no se puede renovar (por ejemplo, sin red) se devuelve el que hay, y el corte lo maneja la reconexión.
 */
export async function obtenerTokenVigente(): Promise<string> {
  if (token && tokenRenovacion) {
    const resto = segundosRestantes(token);
    if (resto !== null && resto < MARGEN_VENCIMIENTO_S) await renovarUnaVez();
  }
  return token ?? '';
}
