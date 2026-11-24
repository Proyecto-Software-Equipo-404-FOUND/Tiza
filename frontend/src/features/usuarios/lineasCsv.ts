// Lee un CSV en el navegador. Se usa solo para mostrar qué dice cada fila que el servidor rechazó:
// el servidor devuelve el número de fila y el motivo, no los datos.
// FileReader (y no File.text) funciona en todos los navegadores y también en las pruebas.
export function leerTexto(archivo: File): Promise<string> {
  return new Promise((resolver) => {
    const lector = new FileReader();
    lector.onload = () => resolver(typeof lector.result === 'string' ? lector.result : '');
    lector.onerror = () => resolver('');
    lector.readAsText(archivo, 'utf-8');
  });
}

/** Las líneas del archivo, numeradas como en cualquier editor: la línea 1 son los títulos. */
export function lineasDelCsv(texto: string): string[] {
  return texto
    .replace(/^\uFEFF/, '')
    .split(/\r?\n/)
    .map((linea) => linea.trim());
}
