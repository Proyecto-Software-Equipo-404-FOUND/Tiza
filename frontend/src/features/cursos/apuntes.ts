// Según contracts/openapi/cursos-v1.json (MaterialCursoDto) y MaterialesController
export type EstadoMaterial = 'Procesando' | 'Listo' | 'Error';

export type Material = {
  id: string;
  cursoId: string;
  titulo: string;
  nombreArchivo: string;
  tamanoBytes: number;
  cantidadPaginas: number;
  estado: EstadoMaterial;
  subidoPorId: string;
  fechaSubidaUtc: string;
};

// Límite del contrato (RN-MAT-03): 10 MB
export const TAMANO_MAXIMO_BYTES = 10 * 1024 * 1024;
export const TITULO_MAXIMO = 150;

// Valida en el navegador antes de enviar. Las 30 páginas y el texto seleccionable solo los sabe el servidor.
export function validarArchivo(archivo: Pick<File, 'name' | 'type' | 'size'>): string | null {
  const esPdf = archivo.type === 'application/pdf' || archivo.name.toLowerCase().endsWith('.pdf');
  if (!esPdf) return 'Solo se aceptan archivos PDF.';
  if (archivo.size === 0) return 'El archivo está vacío.';
  if (archivo.size > TAMANO_MAXIMO_BYTES) return 'El PDF pesa más de 10 MB.';
  return null;
}

export function formatearTamano(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export const etiquetaEstado = (estado: EstadoMaterial): string =>
  estado === 'Listo' ? 'Listo' : estado === 'Procesando' ? 'Procesando' : 'Con error';