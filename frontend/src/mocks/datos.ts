// Los UUID reales y fijos los define Identidad (tarea ID-08). Estos son de juguete.
export const INSTITUCION_A = 'institucion-a';

export const usuarios = [
  { id: 'u-admin-a', nombre: 'Admin A', email: 'admin@a.test', rol: 'administrador', institucionId: INSTITUCION_A },
  { id: 'u-docente-a', nombre: 'Docente A', email: 'docente@a.test', rol: 'docente', institucionId: INSTITUCION_A },
  { id: 'u-alumno1-a', nombre: 'Alumno 1 A', email: 'alumno1@a.test', rol: 'alumno', institucionId: INSTITUCION_A },
  { id: 'u-alumno2-a', nombre: 'Alumno 2 A', email: 'alumno2@a.test', rol: 'alumno', institucionId: INSTITUCION_A },
];

export const CURSO_1_ID = '33333333-0000-0000-0000-000000000001';

export const cursos = [
  {
    id: CURSO_1_ID,
    institucionId: INSTITUCION_A,
    nombre: 'Programación I',
    area: 'Sistemas',
    activo: true,
    fechaCreacionUtc: '2026-10-09T12:00:00Z',
  },
];