import { useRef, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Eye, EyeOff, FileUp, KeyRound } from 'lucide-react';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { Tabla, type Columna } from '@/shared/components/Tabla';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { useAviso } from '@/shared/components/Aviso';
import { importarAlumnos, type ErrorDeFila, type ResultadoImportacion } from './api/usuarios';
import { leerTexto, lineasDelCsv } from './lineasCsv';

// La fila se numera como en el archivo (la 1 son los títulos). `lineas` trae el texto de cada línea para que se vea
// de quién se trata sin tener que contarlas a mano.
const columnasErrores = (lineas: string[]): Columna<ErrorDeFila>[] => [
  { titulo: 'Fila', celda: (error) => error.fila },
  { titulo: 'Datos del archivo', celda: (error) => lineas[error.fila - 1] || '—' },
  { titulo: 'Motivo', celda: (error) => error.motivo },
];

// Con el backend real el motivo de un 400 viene en `title` (por ejemplo «El encabezado debe ser exactamente
// nombre,apellido,email.»). 413: el cuerpo supera el límite del servidor. 415: tipo de archivo no aceptado.
// El resto de los errores usa los mensajes comunes.
function mensajeImportacion(error: unknown) {
  if (error instanceof ApiError) {
    if (error.estado === 400) return error.detalle?.detail ?? error.detalle?.title ?? 'Revisá el archivo y los datos ingresados.';
    if (error.estado === 413) return 'El archivo es demasiado grande. El máximo es 1 MiB.';
    if (error.estado === 415) return 'El archivo tiene que ser un .csv en UTF-8.';
  }
  return mensajeDeError(error);
}

// Mismas reglas que valida Identidad para la contraseña inicial (8 caracteres o más, hasta 72 bytes)
function errorDeContrasena(contrasena: string) {
  if (contrasena === '') return null;
  if (contrasena.trim() === '') return 'No puede tener solo espacios';
  if (contrasena.length < 8) return 'Mínimo 8 caracteres';
  if (new TextEncoder().encode(contrasena).length > 72) return 'Máximo 72 bytes';
  return null;
}

// Ejemplo que se muestra en pantalla: la primera línea son los títulos y cada una de las siguientes, un alumno
const EJEMPLO_CSV = 'nombre,apellido,email\nAna,Paz,ana.paz@correo.com\nLuis,Gómez,luis.gomez@correo.com';

const plural = (cantidad: number, singular: string, pluralizado: string) =>
  `${cantidad} ${cantidad === 1 ? singular : pluralizado}`;

function Resumen({
  resultado,
  lineas,
  conContrasena,
}: {
  resultado: ResultadoImportacion;
  lineas: string[];
  conContrasena: boolean;
}) {
  const { creados, omitidos } = resultado;
  const errores = [...resultado.errores].sort((a, b) => a.fila - b.fila);
  return (
    <div className='flex flex-col gap-3' aria-live='polite'>
      <h3 className='font-heading text-lg font-extrabold text-tiza-bosque'>Resultado de la importación</h3>
      <ul className='flex flex-col gap-1 text-tiza-bosque'>
        <li>
          <span className='font-bold'>{plural(creados, 'alumno creado', 'alumnos creados')}.</span>{' '}
          {creados > 0 &&
            (conContrasena
              ? `Ya ${creados === 1 ? 'puede iniciar sesión' : 'pueden iniciar sesión'} con la contraseña inicial.`
              : `${creados === 1 ? 'Queda pendiente' : 'Quedan pendientes'} de activación: para que puedan entrar, importalos con una contraseña inicial.`)}
        </li>
        {omitidos > 0 && (
          <li>{plural(omitidos, 'fila omitida porque su email ya existía', 'filas omitidas porque su email ya existía')}.</li>
        )}
        <li>{errores.length === 0 ? 'Ninguna fila falló.' : `${plural(errores.length, 'fila falló', 'filas fallaron')}:`}</li>
      </ul>
      {errores.length > 0 && (
        <>
          <Tabla columnas={columnasErrores(lineas)} filas={errores} clave={(error) => `${error.fila}-${error.motivo}`} />
          <p className='text-sm text-tiza-bosque/70'>Las filas se cuentan como en el archivo: la 1 son los títulos.</p>
        </>
      )}
    </div>
  );
}

export function ImportarAlumnosCsv() {
  const cliente = useQueryClient();
  const { mostrar } = useAviso();
  const entrada = useRef<HTMLInputElement>(null);
  const [archivo, setArchivo] = useState<File | null>(null);
  const [errorArchivo, setErrorArchivo] = useState<string | null>(null);
  // Líneas del archivo que se acaba de importar, para mostrar cada fila con error
  const [lineas, setLineas] = useState<string[]>([]);

  const [contrasena, setContrasena] = useState('');
  const [verContrasena, setVerContrasena] = useState(false);
  const [errorContrasena, setErrorContrasena] = useState<string | null>(null);
  // Si el resultado que se ve salió con contraseña inicial (cambia lo que se les dice a los alumnos)
  const [conContrasena, setConContrasena] = useState(false);

  const importar = useMutation({
    mutationFn: ({ archivo: elegido, clave }: { archivo: File; clave?: string }) => importarAlumnos(elegido, clave),
    onSuccess: (resultado) => {
      setArchivo(null);
      setContrasena('');
      if (entrada.current) entrada.current.value = '';
      if (resultado.creados > 0) {
        mostrar('exito', 'Importación terminada.');
        // Actualiza la lista de usuarios y el selector de matrícula
        void cliente.invalidateQueries({ queryKey: ['alumnos'] });
      }
    },
  });

  const alElegir = (elegido: File | null) => {
    importar.reset();
    setLineas([]);
    if (elegido && !elegido.name.toLowerCase().endsWith('.csv')) {
      setArchivo(null);
      setErrorArchivo('Elegí un archivo .csv.');
      return;
    }
    setErrorArchivo(null);
    setArchivo(elegido);
  };

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();
    if (!archivo || importar.isPending) return;
    const motivo = errorDeContrasena(contrasena);
    setErrorContrasena(motivo);
    if (motivo) return;
    setConContrasena(contrasena !== '');
    void leerTexto(archivo).then((texto) => setLineas(lineasDelCsv(texto)));
    importar.mutate({ archivo, clave: contrasena || undefined });
  };

  return (
    <Tarjeta
      titulo='Importar alumnos desde un CSV'
      subtitulo='Creá muchos alumnos a la vez. Hasta 200 alumnos por archivo (sin contar la fila de títulos).'
      icono={<FileUp className='mt-1 h-5 w-5 text-tiza-pasto' aria-hidden='true' />}
    >
      <form onSubmit={enviar} noValidate className='flex flex-col gap-4'>
        <div className='flex flex-col gap-2 text-sm text-tiza-bosque/80'>
          <p>
            El archivo tiene que ser un .csv en UTF-8, de hasta 1 MiB, con las columnas{' '}
            <strong>separadas por comas</strong> (no por punto y coma, como a veces guardan algunos programas) y sin
            líneas en blanco. Tiene que empezar con una fila de títulos, escritos exactamente así:{' '}
            <code className='rounded bg-tiza-menta px-1.5 py-0.5'>nombre,apellido,email</code>. Un email que ya existe
            se omite, y una fila con datos incorrectos no frena a las demás.
          </p>
          <p className='font-semibold text-tiza-bosque'>Ejemplo:</p>
          <pre
            aria-label='Ejemplo de archivo CSV'
            className='overflow-x-auto rounded-xl bg-tiza-menta p-3 text-tiza-bosque'
          >
            {EJEMPLO_CSV}
          </pre>
        </div>

        <div className='flex flex-col gap-1.5'>
          <label htmlFor='alumnos-csv' className='text-sm font-semibold text-tiza-bosque'>
            Archivo CSV
          </label>
          <input
            ref={entrada}
            id='alumnos-csv'
            type='file'
            accept='.csv,text/csv'
            disabled={importar.isPending}
            onChange={(e) => alElegir(e.target.files?.[0] ?? null)}
            aria-invalid={errorArchivo ? true : undefined}
            aria-describedby={errorArchivo ? 'alumnos-csv-error' : undefined}
            className='w-full rounded-xl border border-tiza-borde bg-white p-3 text-tiza-bosque'
          />
          {errorArchivo && (
            <p id='alumnos-csv-error' role='alert' className='text-sm text-tiza-coral'>
              {errorArchivo}
            </p>
          )}
        </div>

        <div className='flex flex-col gap-1.5'>
          <Campo
            etiqueta='Contraseña inicial (opcional)'
            type={verContrasena ? 'text' : 'password'}
            autoComplete='new-password'
            icono={<KeyRound className='h-5 w-5' aria-hidden='true' />}
            accion={
              <button
                type='button'
                onClick={() => setVerContrasena((ver) => !ver)}
                aria-label={verContrasena ? 'Ocultar la contraseña inicial de la importación' : 'Mostrar la contraseña inicial de la importación'}
                className='rounded-md p-1 text-tiza-bosque/70 hover:text-tiza-bosque'
              >
                {verContrasena ? <EyeOff className='h-5 w-5' aria-hidden='true' /> : <Eye className='h-5 w-5' aria-hidden='true' />}
              </button>
            }
            value={contrasena}
            onChange={(evento) => {
              setContrasena(evento.target.value);
              setErrorContrasena(null);
            }}
            error={errorContrasena ?? undefined}
            disabled={importar.isPending}
          />
          <p className='text-sm text-tiza-bosque/70'>
            Si la completás, todos los alumnos nuevos quedan activos y pueden iniciar sesión enseguida con esa
            contraseña. Si la dejás vacía, quedan pendientes hasta activar su cuenta por email, algo que todavía no está
            disponible en esta versión: para que puedan entrar, poné una contraseña inicial. Mínimo 8 caracteres.
          </p>
        </div>

        {importar.isError && <BannerError mensaje={mensajeImportacion(importar.error)} />}

        <Boton type='submit' cargando={importar.isPending} disabled={!archivo} className='self-start'>
          Importar CSV
        </Boton>

        {importar.data && <Resumen resultado={importar.data} lineas={lineas} conContrasena={conContrasena} />}
      </form>
    </Tarjeta>
  );
}
