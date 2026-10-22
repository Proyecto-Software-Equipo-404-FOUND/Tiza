import type { ProblemDetails } from './tipos';

const URL_BASE = import.meta.env.VITE_API_URL ?? '';

let token: string | null = sessionStorage.getItem('token');

export function guardarToken(nuevo: string | null) {
  token = nuevo;
  if (nuevo) sessionStorage.setItem('token', nuevo);
  else sessionStorage.removeItem('token');
}

export const obtenerToken = () => token;

export class ApiError extends Error {
  estado: number;
  detalle: ProblemDetails | null;

  constructor(estado: number, detalle: ProblemDetails | null) {
    super(detalle?.title ?? `Error ${estado}`);
    this.estado = estado;
    this.detalle = detalle;
  }
}

export async function pedir<T>(ruta: string, opciones: RequestInit = {}): Promise<T> {
  const respuesta = await fetch(`${URL_BASE}${ruta}`, {
    ...opciones,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...opciones.headers,
    },
  });

  if (!respuesta.ok) {
    // Con un token guardado, un 401 significa que la sesión venció
    if (respuesta.status === 401 && token) {
      guardarToken(null);
      window.location.assign('/login');
    }
    const detalle = await respuesta.json().catch(() => null);
    throw new ApiError(respuesta.status, detalle);
  }

  if (respuesta.status === 204) return undefined as T;
  return respuesta.json() as Promise<T>;
}
