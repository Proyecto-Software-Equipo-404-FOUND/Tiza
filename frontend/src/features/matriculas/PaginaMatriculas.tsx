import { useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { mensajeDeError } from '@/shared/api/errores';
import { Boton } from '@/shared/components/Boton';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { useAviso } from '@/shared/components/Aviso';
import { listarAlumnos, listarMatriculas, matricularAlumno, type Matricula } from './api/matriculas';

const columnas: Columna<Matricula>[] = [
  { titulo: 'Alumno', celda: (m) => m.alumnoNombre },
  { titulo: 'Estado', celda: (m) => m.estado },
];

export function PaginaMatriculas() {
  const { cohorteId = '' } = useParams<{ cohorteId: string }>();
  const clienteQuery = useQueryClient();
  const { mostrar } = useAviso();
  const [alumnoId, setAlumnoId] = useState('');
  const [errorAlMatricular, setErrorAlMatricular] = useState<string | null>(null);

  const matriculas = useQuery({
    queryKey: ['matriculas', cohorteId],
    queryFn: () => listarMatriculas(cohorteId),
  });
  const alumnos = useQuery({ queryKey: ['alumnos'], queryFn: listarAlumnos });

  const matricular = useMutation({
    mutationFn: (id: string) => matricularAlumno(cohorteId, id),
    onSuccess: () => {
      setAlumnoId('');
      setErrorAlMatricular(null);
      mostrar('exito', 'Alumno matriculado correctamente.');
      void clienteQuery.invalidateQueries({ queryKey: ['matriculas', cohorteId] });
    },
    onError: (error) => setErrorAlMatricular(mensajeDeError(error)),
  });

  const recargar = () => {
    setErrorAlMatricular(null);
    void matriculas.refetch();
    void alumnos.refetch();
  };

  if (matriculas.isPending || alumnos.isPending) return <Spinner texto='Cargando matrícula…' />;

  if (matriculas.isError || alumnos.isError) {
    return <BannerError mensaje={mensajeDeError(matriculas.error ?? alumnos.error)} onReintentar={recargar} />;
  }

  // No se ofrecen los alumnos que ya están matriculados en esta cohorte
  const listaMatriculas = matriculas.data ?? [];
  const yaMatriculados = new Set(listaMatriculas.map((m) => m.alumnoId));
  const disponibles = (alumnos.data ?? []).filter((a) => !yaMatriculados.has(a.id));

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (alumnoId) matricular.mutate(alumnoId);
  };

  return (
    <div className='mx-auto flex max-w-3xl flex-col gap-4 p-4'>
      <Tarjeta titulo='Matricular un alumno'>
        <form onSubmit={enviar} className='flex flex-col gap-3 sm:flex-row sm:items-end'>
          <div className='flex flex-1 flex-col gap-1'>
            <label htmlFor='alumno' className='font-bold text-tiza-pizarra'>
              Alumno
            </label>
            <select
              id='alumno'
              value={alumnoId}
              onChange={(e) => setAlumnoId(e.target.value)}
              disabled={matricular.isPending}
              className='rounded-md border border-tiza-pizarra px-3 py-2 text-tiza-pizarra disabled:opacity-50'
            >
              <option value=''>Elegí un alumno</option>
              {disponibles.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.nombre}
                </option>
              ))}
            </select>
          </div>
          <Boton type='submit' cargando={matricular.isPending} disabled={!alumnoId}>
            Matricular
          </Boton>
        </form>
        {disponibles.length === 0 && (
          <p className='mt-2 text-sm text-tiza-pizarra/70'>No quedan alumnos para matricular.</p>
        )}
      </Tarjeta>

      {errorAlMatricular && <BannerError mensaje={errorAlMatricular} onReintentar={recargar} />}

      <Tarjeta titulo='Alumnos matriculados'>
        <Tabla
          columnas={columnas}
          filas={listaMatriculas}
          clave={(m) => m.id}
          textoVacio='Todavía no hay alumnos matriculados en esta cohorte.'
        />
      </Tarjeta>
    </div>
  );
}
