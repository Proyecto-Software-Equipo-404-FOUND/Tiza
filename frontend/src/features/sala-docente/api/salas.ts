import { pedir } from '@/shared/api/cliente';
import type { SalaCreada } from '../tipos';

// Límites del contrato (contracts/openapi/sala-en-vivo-v1.json, CrearSalaRequest)
export const CAPACIDAD_MINIMA = 1;
export const CAPACIDAD_MAXIMA = 100;
export const CAPACIDAD_POR_DEFECTO = 40;

export type DatosAbrirSala = { evaluacionId: string; capacidadMaxima: number };

// La institución sale del token (claim institucion_id): no se manda ni en el cuerpo ni en un header.
// Responde 201 con { id, codigoPIN, fechaVencimiento, capacidadMaxima }.
export const abrirSala = (datos: DatosAbrirSala) =>
  pedir<SalaCreada>('/api/v1/salas', {
    method: 'POST',
    body: JSON.stringify({ evaluacionId: datos.evaluacionId, capacidadMaxima: datos.capacidadMaxima }),
  });

// Convierte el texto del campo en capacidad válida, o devuelve el error para mostrar
export function validarCapacidad(texto: string): { valor: number | null; error?: string } {
  const limpio = texto.trim();
  if (limpio === '') return { valor: null, error: 'Ingresá la capacidad de la sala.' };
  const numero = Number(limpio);
  if (!Number.isInteger(numero)) return { valor: null, error: 'Ingresá un número entero.' };
  if (numero < CAPACIDAD_MINIMA || numero > CAPACIDAD_MAXIMA) {
    return { valor: null, error: `La capacidad tiene que estar entre ${CAPACIDAD_MINIMA} y ${CAPACIDAD_MAXIMA}.` };
  }
  return { valor: numero };
}