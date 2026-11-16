import { useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { mensajeDeError } from '@/shared/api/errores';
import { Boton } from '@/shared/components/Boton';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { useAviso } from '@/shared/components/Aviso';
import { ModalConfirmacion } from '@/shared/components/ModalConfirmacion';
import {
  darDeBajaMatricula,
  listarAlumnos,
  listarMatriculas,
  matricularAlumno,
  reactivarMatricula,
  type Alumno,
  type Matricula,
} from './api/matriculas';
import { estaDeBaja, fechaCorta, nombreDeAlumno } from './nombres';

const crearColumnas = (alumnos: Alumno[]): Columna<Matricula>[] => [
  { titulo: 'Alumno', celda: (m) => nombreDeAlumno(m, alumnos) },
  { titulo: 'Estado', celda: (m) => m.estado },
  { titulo: 'Desde', celda: (m) => fechaCorta(m.fechaAlta) ?? '—' },
];

// Marco común de la pantalla: así «cargando» y «error» también quedan dentro de <main>
function Marco({ children }: { children: ReactNode }) {
  return (
    <main className='mx-auto flex max-w-3xl flex-col gap-4 p-4'>
      <Link to='/cursos' className='text-sm text-tiza-pasto underline'>
        ← Volver a cursos
      </Link>
      <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Matrícula</h1>
      {children}
    </main>
  );
}

export function PaginaMatriculas() {
  const { cohorteId = '' } = useParams<{ cohorteId: string }>();
  const clienteQuery = useQueryClient();
  const { mostrar } = useAviso();
  const [alumnoId, setAlumnoId] = useState('');
  const [errorAlMatricular, setErrorAlMatricular] = useState<string | null>(null);
  // Un ref se actualiza al instante; el estado de la mutación recién llega en el próximo render
  const enviando = useRef(false);
  const [aDarDeBaja, setADarDeBaja] = useState<Matricula | null>(null);

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
    onSettled: () => {
      enviando.current = false;
    },
  });

  const darDeBaja = useMutation({
    mutationFn: (matricula: Matricula) => darDeBajaMatricula(cohorteId, matricula.id),
    onSuccess: () => {
      mostrar('exito', 'Alumno dado de baja de la cohorte.');
      void clienteQuery.invalidateQueries({ queryKey: ['matriculas', cohorteId] });
    },
    onError: (error) => mostrar('error', mensajeDeError(error)),
    onSettled: () => setADarDeBaja(null),
  });

  const reactivar = useMutation({
    mutationFn: (matricula: Matricula) => reactivarMatricula(cohorteId, matricula.id),
    onSuccess: () => {
      mostrar('exito', 'Matrícula reactivada.');
      void clienteQuery.invalidateQueries({ queryKey: ['matriculas', cohorteId] });
    },
    onError: (error) => mostrar('error', mensajeDeError(error)),
  });

  const recargar = () => {
    setErrorAlMatricular(null);
    void matriculas.refetch();
    void alumnos.refetch();
  };

  // Se espera a ambas listas la primera vez; si la de alumnos falla, no se bloquea la pantalla (ver más abajo)
  if (matriculas.isPending || alumnos.isPending) {
    return (
      <Marco>
        <Spinner texto='Cargando matrícula…' />
      </Marco>
    );
  }

  if (matriculas.isError) {
    return (
      <Marco>
        <BannerError mensaje={mensajeDeError(matriculas.error)} onReintentar={recargar} />
      </Marco>
    );
  }

  // No se ofrecen los alumnos que ya están matriculados en esta cohorte
  const listaMatriculas = matriculas.data ?? [];
  const yaMatriculados = new Set(listaMatriculas.map((m) => m.alumnoId));
  const listaAlumnos = alumnos.data ?? [];
  const disponibles = listaAlumnos.filter((a) => !yaMatriculados.has(a.id));
  const columnas = crearColumnas(listaAlumnos);
  const columnasActivas: Columna<Matricula>[] = [
    ...columnas,
    {
      titulo: 'Acción',
      celda: (m) => (
        <Boton
          type='button'
          variante='secundario'
          className='!px-3 !py-1 text-sm'
          disabled={darDeBaja.isPending}
          onClick={() => setADarDeBaja(m)}
        >
          Dar de baja
        </Boton>
      ),
    },
  ];
  const columnasDeBaja: Columna<Matricula>[] = [
    ...columnas,
    {
      titulo: 'Acción',
      celda: (m) => (
        <Boton
          type='button'
          variante='secundario'
          className='!px-3 !py-1 text-sm'
          cargando={reactivar.isPending && reactivar.variables?.id === m.id}
          disabled={reactivar.isPending}
          onClick={() => reactivar.mutate(m)}
        >
          Reactivar
        </Boton>
      ),
    },
  ];
  const activas = listaMatriculas.filter((m) => !estaDeBaja(m));
  const deBaja = listaMatriculas.filter(estaDeBaja);

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    // Si ya hay un envío en curso (por ejemplo, Enter dos veces), no se envía de nuevo
    if (!alumnoId || enviando.current) return;
    enviando.current = true;
    matricular.mutate(alumnoId);
  };

  return (
    <Marco>
      <Tarjeta titulo='Matricular un alumno'>
        <form onSubmit={enviar} className='flex flex-col gap-3 sm:flex-row sm:items-end'>
          <div className='flex flex-1 flex-col gap-1'>
            <label htmlFor='alumno' className='font-bold text-tiza-bosque'>
              Alumno
            </label>
            <select
              id='alumno'
              value={alumnoId}
              onChange={(e) => setAlumnoId(e.target.value)}
              disabled={matricular.isPending || alumnos.isError}
              className='rounded-xl border border-tiza-borde bg-white px-3 py-3 text-tiza-bosque disabled:opacity-50'
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
        {alumnos.isSuccess && disponibles.length === 0 && (
          <p className='mt-2 text-sm text-tiza-bosque/70'>No quedan alumnos para matricular.</p>
        )}
        {/* Sin la lista no se puede elegir a quién matricular, pero los matriculados se siguen viendo */}
        {alumnos.isError && (
          <div className='mt-3'>
            <BannerError
              mensaje='No pudimos cargar la lista de alumnos para matricular. Los matriculados se muestran igual.'
              onReintentar={() => void alumnos.refetch()}
            />
          </div>
        )}
      </Tarjeta>

      {errorAlMatricular && <BannerError mensaje={errorAlMatricular} onReintentar={recargar} />}

      <Tarjeta titulo='Alumnos matriculados'>
        <Tabla
          columnas={columnasActivas}
          filas={activas}
          clave={(m) => m.id}
          textoVacio='Todavía no hay alumnos matriculados en esta cohorte.'
        />
      </Tarjeta>

      <Tarjeta titulo='Matrículas inactivas' subtitulo='Alumnos dados de baja de esta cohorte.'>
        <Tabla
          columnas={columnasDeBaja}
          filas={deBaja}
          clave={(m) => m.id}
          textoVacio='No hay matrículas inactivas.'
        />
      </Tarjeta>

      <ModalConfirmacion
        abierto={aDarDeBaja !== null}
        titulo='Dar de baja al alumno'
        mensaje={
          aDarDeBaja
            ? `${nombreDeAlumno(aDarDeBaja, listaAlumnos)} deja de estar matriculado en esta cohorte y ya no va a poder entrar a sus salas en vivo.`
            : ''
        }
        textoConfirmar='Sí, dar de baja'
        onConfirmar={() => aDarDeBaja && !darDeBaja.isPending && darDeBaja.mutate(aDarDeBaja)}
        onCancelar={() => setADarDeBaja(null)}
      />
    </Marco>
  );
}
