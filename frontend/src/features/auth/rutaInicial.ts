import type { Rol } from '@/shared/api/tipos';

// Decisión del PO (12/11): todos los roles entran por Inicio; desde el menú van a cada sección.
export const rutaInicial = (_rol: Rol) => '/';
