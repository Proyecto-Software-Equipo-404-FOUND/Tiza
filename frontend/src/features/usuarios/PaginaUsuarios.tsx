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
import { useAviso } from '@/shared/components/Aviso';
import { listarUsuarios, registrarUsuario, type RolNuevo, type UsuarioDeLaLista } from './api/usuarios';

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

const columnas: Columna<UsuarioDeLaLista>[] = [
  { titulo: 'Nombre', celda: (usuario) => <span className='font-semibold'>{usuario.nombre}</span> },
  { titulo: 'Email', celda: (usuario) => usuario.email },
  { titulo: 'Rol', celda: (usuario) => usuario.rol },
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

  const alumnos = useQuery({ queryKey: ['alumnos', 'detalle'], queryFn: () => listarUsuarios('Alumno') });
  const docentes = useQuery({ queryKey: ['docentes', 'detalle'], queryFn: () => listarUsuarios('Docente') });
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
            columnas={columnas}
            filas={usuarios}
            clave={(usuario) => usuario.id}
            textoVacio='Todavía no hay usuarios.'
          />
        )}
      </Tarjeta>
    </main>
  );
}
