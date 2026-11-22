export type Rol = 'alumno' | 'docente' | 'administrador' | 'invitado';

export type Usuario = {
  id: string;
  nombre: string;
  rol: Rol;
  institucionId: string;
};

export type Curso = { id: string; institucionId: string; nombre: string; area: string; activo: boolean; fechaCreacionUtc: string; };

export type ResultadoPaginado<T> = { items: T[]; totalItems: number; pagina: number; tamanoPagina: number; totalPaginas: number; };


export type Cohorte = { id: string; cursoId: string; docenteId: string; nombre: string; periodo: string; activo: boolean; fechaCreacionUtc: string; };

// `type` lleva el código del error de negocio (por ejemplo NOTA_YA_APROBADA)
export type ProblemDetails = { title?: string; status: number; detail?: string; type?: string };