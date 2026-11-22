import { useRef, useState, type FormEvent } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { FileUp } from 'lucide-react';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
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

// 415: el servidor no aceptó el tipo de archivo; el resto de los errores usa los mensajes comunes
const mensajeImportacion = (error: unknown) =>
  error instanceof ApiError && error.estado === 415
    ? 'El archivo tiene que ser un .csv en UTF-8.'
    : mensajeDeError(error);

// Ejemplo que se muestra en pantalla: la primera línea son los títulos y cada una de las siguientes, un alumno
const EJEMPLO_CSV = 'nombre,apellido,email\nAna,Paz,ana.paz@correo.com\nLuis,Gómez,luis.gomez@correo.com';

const plural = (cantidad: number, singular: string, pluralizado: string) =>
  `${cantidad} ${cantidad === 1 ? singular : pluralizado}`;

function Resumen({ resultado, lineas }: { resultado: ResultadoImportacion; lineas: string[] }) {
  const { creados, omitidos } = resultado;
  const errores = [...resultado.errores].sort((a, b) => a.fila - b.fila);
  return (
    <div className='flex flex-col gap-3' aria-live='polite'>
      <h3 className='font-heading text-lg font-extrabold text-tiza-bosque'>Resultado de la importación</h3>
      <ul className='flex flex-col gap-1 text-tiza-bosque'>
        <li>
          <span className='font-bold'>{plural(creados, 'alumno creado', 'alumnos creados')}.</span>{' '}
          {creados > 0 &&
            `${creados === 1 ? 'Recibe' : 'Reciben'} un email para definir su contraseña.`}
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

  const importar = useMutation({
    mutationFn: importarAlumnos,
    onSuccess: (resultado) => {
      setArchivo(null);
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
    void leerTexto(archivo).then((texto) => setLineas(lineasDelCsv(texto)));
    importar.mutate(archivo);
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
            El archivo tiene que estar en UTF-8 y empezar con una fila de títulos:{' '}
            <code className='rounded bg-tiza-menta px-1.5 py-0.5'>nombre,apellido,email</code>. Un email que ya existe
            se omite, y una fila con error no frena a las demás. Cada alumno nuevo recibe un email para definir su
            contraseña.
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

        {importar.isError && <BannerError mensaje={mensajeImportacion(importar.error)} />}

        <Boton type='submit' cargando={importar.isPending} disabled={!archivo} className='self-start'>
          Importar CSV
        </Boton>

        {importar.data && <Resumen resultado={importar.data} lineas={lineas} />}
      </form>
    </Tarjeta>
  );
}
