export type Rol = 'alumno' | 'docente' | 'administrador';

export type Usuario = {
  id: string;
  nombre: string;
  rol: Rol;
  institucionId: string;
};

export type Curso = { id: string; nombre: string; institucionId: string };

export type Cohorte = { id: string; cursoId: string; nombre: string; docenteId: string };

export type ProblemDetails = { title?: string; status: number; detail?: string };
