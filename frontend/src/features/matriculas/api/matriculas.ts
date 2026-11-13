import { pedir } from '@/shared/api/cliente';

export type Alumno = { id: string; nombre: string };

// Según MatriculacionDto de contracts/openapi/cursos-v1.json. El servidor no devuelve el nombre del alumno
// (vive en Identidad), por eso `alumnoNombre` es opcional: ver nombres.ts.
export type Matricula = {
  id: string;
  institucionId?: string;
  cohorteId: string;
  alumnoId: string;
  fechaAlta?: string;
  // El contrato lo define como texto libre (hoy: Activa, Baja…)
  estado: string;
  alumnoNombre?: string;
};

// Por defecto el servidor devuelve solo las activas (soloActivas=true); se piden todas para poder
// mostrar aparte las dadas de baja.
export const listarMatriculas = (cohorteId: string) =>
  pedir<Matricula[]>(`/api/v1/cohortes/${cohorteId}/matriculas?soloActivas=false`);

export const matricularAlumno = (cohorteId: string, alumnoId: string) =>
  pedir<Matricula>(`/api/v1/cohortes/${cohorteId}/matriculas`, {
    method: 'POST',
    body: JSON.stringify({ alumnoId }),
  });

// Baja lógica: la matrícula pasa a estado Baja y queda en «Matrículas inactivas». 204 sin cuerpo.
export const darDeBajaMatricula = (cohorteId: string, matriculaId: string) =>
  pedir<void>(`/api/v1/cohortes/${cohorteId}/matriculas/${matriculaId}`, { method: 'DELETE' });

// Vuelve a activar una matrícula dada de baja (POST …/reactivar, 204). 409 si el estado no lo permite.
export const reactivarMatricula = (cohorteId: string, matriculaId: string) =>
  pedir<void>(`/api/v1/cohortes/${cohorteId}/matriculas/${matriculaId}/reactivar`, { method: 'POST' });

// Forma de UsuarioResumen en contracts/openapi/identidad-v1.json (GET /api/usuarios, S2-ID-10)
type UsuarioResumen = { id: string; nombre: string; apellido: string; email: string; rol: string };

// Alumnos activos de la institución del token (solo Docente o Administrador). Por el gateway la ruta es
// /api/v1/identidad/usuarios. El `id` es el mismo AlumnoId que usa Cursos en las matrículas.
export async function listarAlumnos(): Promise<Alumno[]> {
  const usuarios = await pedir<UsuarioResumen[]>('/api/v1/identidad/usuarios?rol=Alumno');
  return usuarios.map((u) => ({ id: u.id, nombre: `${u.nombre} ${u.apellido}`.trim() }));
}
