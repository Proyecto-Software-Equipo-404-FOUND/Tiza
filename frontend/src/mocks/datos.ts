// UUID fijos de la matriz canónica de ID-08 (docs de Identidad) y de las semillas de Cursos.
export const INSTITUCION_A = '11111111-1111-1111-1111-111111111111';

export const ADMIN_A_ID = '08000001-0000-4000-8000-000000000001';
export const DOCENTE_A_ID = '11111111-0000-0000-0000-000000000002';
export const ALUMNO_1_A_ID = '11111111-0000-0000-0000-000000000003';
export const ALUMNO_2_A_ID = '11111111-0000-0000-0000-000000000004';

export const usuarios = [
  { id: ADMIN_A_ID, nombre: 'Admin A', email: 'admin@a.test', rol: 'administrador', institucionId: INSTITUCION_A },
  { id: DOCENTE_A_ID, nombre: 'Docente A', email: 'docente@a.test', rol: 'docente', institucionId: INSTITUCION_A },
  { id: ALUMNO_1_A_ID, nombre: 'Alumno 1 A', email: 'alumno1@a.test', rol: 'alumno', institucionId: INSTITUCION_A },
  { id: ALUMNO_2_A_ID, nombre: 'Alumno 2 A', email: 'alumno2@a.test', rol: 'alumno', institucionId: INSTITUCION_A },
];

// Curso A1 y cohorte A1 de las semillas de Cursos
export const CURSO_1_ID = 'cccccccc-0000-0000-0000-000000000001';
export const COHORTE_1_ID = '33333333-3333-3333-3333-333333333333';

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