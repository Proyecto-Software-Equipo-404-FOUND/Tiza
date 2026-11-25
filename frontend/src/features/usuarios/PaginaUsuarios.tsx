import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { Eye, EyeOff, KeyRound, Mail, ShieldCheck, UserRound } from 'lucide-react';
import { z } from 'zod';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { CampoSelect } from '@/shared/components/CampoSelect';
import { BannerError } from '@/shared/components/BannerError';
import { Spinner } from '@/shared/components/Spinner';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { ModalConfirmacion } from '@/shared/components/ModalConfirmacion';
import { useAviso } from '@/shared/components/Aviso';
import {
  darDeBajaUsuario,
  listarUsuarios,
  reactivarUsuario,
  registrarUsuario,
  type RolNuevo,
  type UsuarioDeLaLista,
} from './api/usuarios';
import { ImportarAlumnosCsv } from './ImportarAlumnosCsv';

// Mismos límites que valida Identidad (RegisterRequest). BCrypt corta la contraseña en 72 bytes.
const esquema = z.object({
  nombre: z.string().trim().min(1, 'Ingresá un nombre').max(100, 'Máximo 100 caracteres'),
  apellido: z.string().trim().min(1, 'Ingresá un apellido').max(100, 'Máximo 100 caracteres'),
  email: z.string().trim().min(1, 'Ingresá un email').email('Ingresá un email válido').max(150, 'Máximo 150 caracteres'),
  password: z
    .string()
    .min(8, 'Mínimo 8 caracteres')
    .refine((valor) => new TextEncoder().encode(valor).length <= 72, 'Máximo 72 bytes'),
});

type Campos = z.infer<typeof esquema>;
type Errores = Partial<Record<keyof Campos | 'confirmacion' | 'confirmacionEmail', string>>;

const VACIO: Campos = { nombre: '', apellido: '', email: '', password: '' };

type AccionEstado = { usuario: UsuarioDeLaLista; accion: 'baja' | 'reactivar' };

const columnasUsuarios = (onAccion: (accion: AccionEstado) => void): Columna<UsuarioDeLaLista>[] => [
  // En el celular la tabla tiene que entrar sin desplazarse: los textos largos (sobre todo el email) se parten en líneas
  { titulo: 'Nombre', celda: (usuario) => <span className='break-words font-semibold'>{usuario.nombre}</span> },
  { titulo: 'Email', celda: (usuario) => <span className='break-all'>{usuario.email}</span> },
  { titulo: 'Rol', celda: (usuario) => usuario.rol },
  {
    titulo: 'Estado',
    celda: (usuario) =>
      usuario.pendienteActivacion ? (
        <span className='rounded-full bg-amber-100 px-3 py-1 text-xs font-bold text-amber-900'>Pendiente de activación</span>
      ) : usuario.activo === false ? (
        <span className='rounded-full bg-tiza-pizarra/10 px-3 py-1 text-xs font-bold text-tiza-pizarra'>Dado de baja</span>
      ) : (
        <span className='rounded-full bg-tiza-menta px-3 py-1 text-xs font-bold text-tiza-bosque'>Activo</span>
      ),
  },
  {
    titulo: 'Acción',
    celda: (usuario) =>
      usuario.pendienteActivacion ? (
        // Sin contraseña no puede entrar aunque se lo active: hay que volver a importarlo con contraseña inicial
        <span className='text-xs text-tiza-bosque/70'>Importalo de nuevo con contraseña inicial</span>
      ) : usuario.activo === false ? (
        <Boton type='button' variante='secundario' onClick={() => onAccion({ usuario, accion: 'reactivar' })}>
          Reactivar
        </Boton>
      ) : (
        <Boton type='button' variante='peligro' onClick={() => onAccion({ usuario, accion: 'baja' })}>
          Dar de baja
        </Boton>
      ),
  },
];

const AVISO_CREADO: Record<RolNuevo, string> = {
  Alumno: 'Alumno creado. Ya podés matricularlo desde la cohorte.',
  Docente: 'Docente creado. Ya podés asignarlo a una cohorte.',
};

export function PaginaUsuarios() {
  const cliente = useQueryClient();
  const { mostrar } = useAviso();
  const [datos, setDatos] = useState<Campos>(VACIO);
  const [confirmacion, setConfirmacion] = useState('');
  const [confirmacionEmail, setConfirmacionEmail] = useState('');
  const [verContrasena, setVerContrasena] = useState(false);
  const [errores, setErrores] = useState<Errores>({});

  const [rol, setRol] = useState<RolNuevo>('Alumno');

  const alumnos = useQuery({ queryKey: ['alumnos', 'detalle'], queryFn: () => listarUsuarios('Alumno', true) });
  const docentes = useQuery({ queryKey: ['docentes', 'detalle'], queryFn: () => listarUsuarios('Docente', true) });
  const [pendiente, setPendiente] = useState<AccionEstado | null>(null);
  const cambiarEstado = useMutation({
    mutationFn: ({ usuario, accion }: AccionEstado) =>
      accion === 'baja' ? darDeBajaUsuario(usuario.id) : reactivarUsuario(usuario.id),
    onSuccess: (_resultado, { usuario, accion }) => {
      mostrar('exito', accion === 'baja' ? `${usuario.nombre} quedó dado de baja.` : `${usuario.nombre} fue reactivado.`);
      // Las otras pantallas (matrículas, selector de docentes) usan sus propias listas de activos
      void cliente.invalidateQueries({ queryKey: ['alumnos'] });
      void cliente.invalidateQueries({ queryKey: ['docentes'] });
    },
    onError: (falla) => mostrar('error', mensajeDeError(falla)),
  });
  const cargando = alumnos.isPending || docentes.isPending;
  const falla = alumnos.error ?? docentes.error;
  const usuarios = [...(docentes.data ?? []), ...(alumnos.data ?? [])];

  const crear = useMutation({
    mutationFn: registrarUsuario,
    onSuccess: (_resultado, enviado) => {
      setDatos(VACIO);
      setConfirmacion('');
      setConfirmacionEmail('');
      mostrar('exito', AVISO_CREADO[enviado.rol]);
      // Los prefijos también actualizan el selector de matrícula (alumnos) y el de cohortes (docentes)
      void cliente.invalidateQueries({ queryKey: [enviado.rol === 'Alumno' ? 'alumnos' : 'docentes'] });
    },
  });

  const cambiar = (campo: keyof Campos) => (evento: { target: { value: string } }) =>
    setDatos((previos) => ({ ...previos, [campo]: evento.target.value }));

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();
    if (crear.isPending) return;

    const resultado = esquema.safeParse(datos);
    const nuevos: Errores = {};
    if (!resultado.success) {
      for (const problema of resultado.error.issues) {
        const campo = problema.path[0] as keyof Campos;
        nuevos[campo] ??= problema.message;
      }
    }
    if (confirmacionEmail.trim().toLowerCase() !== datos.email.trim().toLowerCase()) {
      nuevos.confirmacionEmail = 'Los emails no coinciden';
    }
    if (confirmacion !== datos.password) nuevos.confirmacion = 'Las contraseñas no coinciden';
    if (!resultado.success || nuevos.confirmacion || nuevos.confirmacionEmail) {
      setErrores(nuevos);
      return;
    }

    setErrores({});
    crear.mutate({ ...resultado.data, rol });
  };

  const emailRepetido = crear.error instanceof ApiError && crear.error.estado === 409;

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-4 p-4'>
      <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Usuarios</h1>
      <p className='text-tiza-bosque/80'>
        Creá las cuentas de alumnos y docentes de tu institución. Los alumnos se matriculan desde{' '}
        <Link to='/cursos' className='text-tiza-pasto underline'>
          Cursos
        </Link>
        , en la matrícula de cada cohorte; los docentes se asignan al crear o editar la cohorte.
      </p>

      <Tarjeta titulo='Crear usuario' subtitulo='Ingresa con este email y la contraseña inicial.'>
        <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
          <CampoSelect
            etiqueta='Rol'
            icono={<ShieldCheck className='h-5 w-5' aria-hidden='true' />}
            value={rol}
            onChange={(evento) => setRol(evento.target.value as RolNuevo)}
            disabled={crear.isPending}
          >
            <option value='Alumno'>Alumno</option>
            <option value='Docente'>Docente</option>
          </CampoSelect>

          <div className='grid gap-5 md:grid-cols-2'>
            <Campo
              etiqueta='Nombre'
              icono={<UserRound className='h-5 w-5' aria-hidden='true' />}
              value={datos.nombre}
              onChange={cambiar('nombre')}
              error={errores.nombre}
              disabled={crear.isPending}
            />
            <Campo
              etiqueta='Apellido'
              icono={<UserRound className='h-5 w-5' aria-hidden='true' />}
              value={datos.apellido}
              onChange={cambiar('apellido')}
              error={errores.apellido}
              disabled={crear.isPending}
            />
          </div>
          <div className='grid gap-5 md:grid-cols-2'>
            <Campo
              etiqueta='Email'
              type='email'
              autoComplete='off'
              icono={<Mail className='h-5 w-5' aria-hidden='true' />}
              value={datos.email}
              onChange={cambiar('email')}
              error={errores.email}
              disabled={crear.isPending}
            />
            <Campo
              etiqueta='Repetir email'
              type='email'
              autoComplete='off'
              icono={<Mail className='h-5 w-5' aria-hidden='true' />}
              value={confirmacionEmail}
              onChange={(evento) => setConfirmacionEmail(evento.target.value)}
              error={errores.confirmacionEmail}
              disabled={crear.isPending}
            />
          </div>
          <div className='grid gap-5 md:grid-cols-2'>
            <Campo
              etiqueta='Contraseña inicial'
              type={verContrasena ? 'text' : 'password'}
              autoComplete='new-password'
              icono={<KeyRound className='h-5 w-5' aria-hidden='true' />}
              accion={
                <button
                  type='button'
                  onClick={() => setVerContrasena((ver) => !ver)}
                  aria-label={verContrasena ? 'Ocultar las contraseñas' : 'Mostrar las contraseñas'}
                  className='rounded-md p-1 text-tiza-bosque/70 hover:text-tiza-bosque'
                >
                  {verContrasena ? (
                    <EyeOff className='h-5 w-5' aria-hidden='true' />
                  ) : (
                    <Eye className='h-5 w-5' aria-hidden='true' />
                  )}
                </button>
              }
              value={datos.password}
              onChange={cambiar('password')}
              error={errores.password}
              disabled={crear.isPending}
            />
            <Campo
              etiqueta='Repetir contraseña'
              type={verContrasena ? 'text' : 'password'}
              autoComplete='new-password'
              icono={<KeyRound className='h-5 w-5' aria-hidden='true' />}
              value={confirmacion}
              onChange={(evento) => setConfirmacion(evento.target.value)}
              error={errores.confirmacion}
              disabled={crear.isPending}
            />
          </div>

          {crear.isError && (
            <BannerError
              mensaje={emailRepetido ? 'Ya existe un usuario con ese email.' : mensajeDeError(crear.error)}
            />
          )}

          <Boton type='submit' cargando={crear.isPending} className='self-start'>
            Crear {rol === 'Alumno' ? 'alumno' : 'docente'}
          </Boton>
        </form>
      </Tarjeta>

      <ImportarAlumnosCsv />

      <Tarjeta titulo='Usuarios de la institución'>
        {cargando ? (
          <Spinner />
        ) : falla ? (
          <BannerError
            mensaje={mensajeDeError(falla)}
            onReintentar={() => {
              void alumnos.refetch();
              void docentes.refetch();
            }}
          />
        ) : (
          <Tabla
            columnas={columnasUsuarios(setPendiente)}
            filas={usuarios}
            clave={(usuario) => usuario.id}
            textoVacio='Todavía no hay usuarios.'
          />
        )}
      </Tarjeta>

      <ModalConfirmacion
        abierto={pendiente !== null}
        titulo={pendiente?.accion === 'reactivar' ? '¿Reactivar al usuario?' : '¿Dar de baja al usuario?'}
        mensaje={
          pendiente?.accion === 'reactivar'
            ? `${pendiente.usuario.nombre} va a poder iniciar sesión de nuevo.`
            : `${pendiente?.usuario.nombre ?? ''} no va a poder iniciar sesión y se cierran sus sesiones. No se borran sus datos y lo podés reactivar.`
        }
        textoConfirmar={pendiente?.accion === 'reactivar' ? 'Sí, reactivar' : 'Sí, dar de baja'}
        variante={pendiente?.accion === 'reactivar' ? 'principal' : 'peligro'}
        onConfirmar={() => {
          if (pendiente) cambiarEstado.mutate(pendiente);
          setPendiente(null);
        }}
        onCancelar={() => setPendiente(null)}
      />
    </main>
  );
}
