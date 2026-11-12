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

export const listarMatriculas = (cohorteId: string) =>
  pedir<Matricula[]>(`/api/v1/cohortes/${cohorteId}/matriculas`);

export const matricularAlumno = (cohorteId: string, alumnoId: string) =>
  pedir<Matricula>(`/api/v1/cohortes/${cohorteId}/matriculas`, {
    method: 'POST',
    body: JSON.stringify({ alumnoId }),
  });

// Forma de UsuarioResumen en contracts/openapi/identidad-v1.json (GET /api/usuarios, S2-ID-10)
type UsuarioResumen = { id: string; nombre: string; apellido: string; email: string; rol: string };

// Alumnos activos de la institución del token (solo Docente o Administrador). Por el gateway la ruta es
// /api/v1/identidad/usuarios. El `id` es el mismo AlumnoId que usa Cursos en las matrículas.
export async function listarAlumnos(): Promise<Alumno[]> {
  const usuarios = await pedir<UsuarioResumen[]>('/api/v1/identidad/usuarios?rol=Alumno');
  return usuarios.map((u) => ({ id: u.id, nombre: `${u.nombre} ${u.apellido}`.trim() }));
}
