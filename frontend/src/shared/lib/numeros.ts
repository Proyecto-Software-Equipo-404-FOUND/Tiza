const NUMERO = new Intl.NumberFormat('es-AR', { maximumFractionDigits: 2 });

/** 3.5 → «3,5». */
export const formatearNumero = (valor: number) => NUMERO.format(valor);

/** «3,5 / 4». Con el máximo en null o sin nota devuelve null: quien llama decide qué mostrar. */
export function formatearNota(nota: number | null | undefined, maximo: number | null | undefined) {
  if (nota == null) return null;
  return maximo == null ? formatearNumero(nota) : `${formatearNumero(nota)} / ${formatearNumero(maximo)}`;
}
