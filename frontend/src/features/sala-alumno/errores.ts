import type { CodigoSala } from './tipos';

const CODIGOS: CodigoSala[] = [
  'NO_AUTENTICADO',
  'NO_AUTORIZADO',
  'SALA_NO_ENCONTRADA',
  'SALA_OTRA_INSTITUCION',
  'SALA_LLENA',
  'ALUMNO_YA_EN_SALA',
  'SALA_FINALIZADA',
];

// Mensajes que el hub devuelve hoy sin código (sección 8.4 del contrato). Es un puente
// temporal: cuando el servidor emita «CODIGO: mensaje» se puede borrar.
const MENSAJES_ACTUALES: Array<[string, CodigoSala]> = [
  ['Alumno no identificado', 'NO_AUTENTICADO'],
  ['Institución no válida', 'NO_AUTORIZADO'],
  ['No tienes permiso para ingresar', 'SALA_OTRA_INSTITUCION'],
  ['no existe, está inactiva o venció', 'SALA_NO_ENCONTRADA'],
  ['ya está registrado en la sala', 'ALUMNO_YA_EN_SALA'],
  ['capacidad máxima', 'SALA_LLENA'],
];

/** Traduce el error de una invocación al hub en un código estable del contrato. */
export function codigoDeError(error: unknown): CodigoSala {
  const texto = error instanceof Error ? error.message : String(error);

  // SignalR puede anteponer texto al mensaje del servidor, por eso se busca con includes
  const porCodigo = CODIGOS.find((codigo) => texto.includes(codigo));
  if (porCodigo) return porCodigo;

  const legado = MENSAJES_ACTUALES.find(([fragmento]) => texto.includes(fragmento));
  return legado ? legado[1] : 'ERROR_INTERNO';
}

const MENSAJES: Record<CodigoSala, string> = {
  NO_AUTENTICADO: 'Tu sesión venció. Ingresá de nuevo.',
  NO_AUTORIZADO: 'No tenés permiso para entrar a esta sala.',
  SALA_NO_ENCONTRADA: 'No encontramos una sala con ese PIN. Revisalo e intentá de nuevo.',
  // No se revela por qué: la sala es de otra institución
  SALA_OTRA_INSTITUCION: 'No podés entrar a esta sala.',
  SALA_LLENA: 'La sala está llena. Avisale a tu docente.',
  ALUMNO_YA_EN_SALA: 'Ya estás en esta sala.',
  SALA_FINALIZADA: 'Esta sala ya finalizó.',
  ERROR_INTERNO: 'Algo salió mal. Probá de nuevo.',
};

export const mensajeDeCodigo = (codigo: CodigoSala) => MENSAJES[codigo];
