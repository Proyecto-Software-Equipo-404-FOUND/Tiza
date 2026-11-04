/** UUID v4. Usa el del navegador y, si no existe (contextos no seguros), arma uno con Math.random. */
export function nuevoUuid(): string {
  const nativo = globalThis.crypto?.randomUUID?.();
  if (nativo) return nativo;
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const n = Math.floor(Math.random() * 16);
    return (c === 'x' ? n : (n & 0x3) | 0x8).toString(16);
  });
}
