// Los UUID reales y fijos los define Identidad (tarea ID-08). Estos son de juguete.
export const INSTITUCION_A = 'institucion-a';

export const usuarios = [
  { id: 'u-admin-a', nombre: 'Admin A', email: 'admin@a.test', rol: 'administrador', institucionId: INSTITUCION_A },
  { id: 'u-docente-a', nombre: 'Docente A', email: 'docente@a.test', rol: 'docente', institucionId: INSTITUCION_A },
  { id: 'u-alumno1-a', nombre: 'Alumno 1 A', email: 'alumno1@a.test', rol: 'alumno', institucionId: INSTITUCION_A },
  { id: 'u-alumno2-a', nombre: 'Alumno 2 A', email: 'alumno2@a.test', rol: 'alumno', institucionId: INSTITUCION_A },
];

export const cursos = [{ id: 'c-1', nombre: 'Programación I', institucionId: INSTITUCION_A }];