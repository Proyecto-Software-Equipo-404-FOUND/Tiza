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