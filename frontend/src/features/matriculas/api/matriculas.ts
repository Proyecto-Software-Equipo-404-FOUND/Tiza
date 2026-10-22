import { pedir } from '@/shared/api/cliente';

// Provisorio: el backend (CU-04) todavía no define estas respuestas. El nombre del alumno vive en
// Identidad, así que hay que confirmar con Cursos e Identidad cómo llega a esta pantalla.
export type Alumno = { id: string; nombre: string };

export type Matricula = {
  id: string;
  cohorteId: string;
  alumnoId: string;
  alumnoNombre: string;
  estado: 'Activa' | 'Baja' | 'Completada';
};

export const listarMatriculas = (cohorteId: string) =>
  pedir<Matricula[]>(`/api/v1/cohortes/${cohorteId}/matriculas`);

export const matricularAlumno = (cohorteId: string, alumnoId: string) =>
  pedir<Matricula>(`/api/v1/cohortes/${cohorteId}/matriculas`, {
    method: 'POST',
    body: JSON.stringify({ alumnoId }),
  });

// Provisorio: de dónde sale la lista de alumnos para elegir lo define el backend.
export const listarAlumnos = () => pedir<Alumno[]>('/api/v1/identidad/alumnos');
