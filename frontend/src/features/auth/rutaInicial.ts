import type { Rol } from '@/shared/api/tipos';

// Docente y administrador van a /cursos. Qué ve el alumno es una pregunta pendiente para el PO.
export const rutaInicial = (rol: Rol) => (rol === 'alumno' ? '/' : '/cursos');
