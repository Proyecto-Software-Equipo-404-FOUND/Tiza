import { useRef, useState, type FormEvent } from 'react';
import { Upload, FileText, Cloud } from 'lucide-react';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { mensajeApuntes } from '../mensajesApuntes';
import { TITULO_MAXIMO, formatearTamano, validarArchivo } from '../apuntes';
import { useSubirMaterial } from '../hooks/useSubirMaterial';
import { useGoogleDrivePicker } from '../hooks/useGoogleDrivePicker';

type Props = { cursoId: string };

type OrigenArchivo = 'local' | 'drive';

export function FormularioApunte({ cursoId }: Props) {
  const subir = useSubirMaterial(cursoId);
  const { mostrar } = useAviso();
  const { isGoogleDriveConfigurado, isDescargando, abrirPicker } = useGoogleDrivePicker();

  const [origen, setOrigen] = useState<OrigenArchivo>('local');
  const [archivo, setArchivo] = useState<File | null>(null);
  const [titulo, setTitulo] = useState('');
  const [errorArchivo, setErrorArchivo] = useState<string | undefined>();
  const entradaArchivo = useRef<HTMLInputElement>(null);

  // Al cambiar de pestaña se resetea el archivo y sus errores, preservando el título intacto
  const cambiarOrigen = (nuevoOrigen: OrigenArchivo) => {
    if (nuevoOrigen === origen) return;
    setOrigen(nuevoOrigen);
    setArchivo(null);
    setErrorArchivo(undefined);
    if (entradaArchivo.current) entradaArchivo.current.value = '';
  };

  const alElegir = (elegido: File | null) => {
    setArchivo(elegido);
    setErrorArchivo(elegido ? (validarArchivo(elegido) ?? undefined) : undefined);
  };

  const handleElegirDeDrive = () => {
    abrirPicker({
      onArchivoSeleccionado: (archivoDescargado) => {
        alElegir(archivoDescargado);
      },
      onError: (mensaje) => {
        // Errores de validación sobre el PDF se muestran en línea
        if (mensaje.includes('PDF') || mensaje.includes('10 MB') || mensaje.includes('vacío')) {
          setErrorArchivo(mensaje);
        } else {
          // Errores de OAuth, popup o conexión se muestran mediante el aviso flotante
          mostrar('error', mensaje);
        }
      },
    });
  };

  const alEnviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (!archivo) {
      setErrorArchivo('Elegí un archivo PDF.');
      return;
    }
    const problema = validarArchivo(archivo);
    if (problema) {
      setErrorArchivo(problema);
      return;
    }
    subir.mutate(
      { archivo, titulo },
      {
        onSuccess: () => {
          mostrar('exito', 'Apunte subido.');
          setArchivo(null);
          setTitulo('');
          setErrorArchivo(undefined);
          if (entradaArchivo.current) entradaArchivo.current.value = '';
        },
        // El 403 ya lo avisa el manejo global (Proveedores): acá no se repite
        onError: (error) => {
          if (!(error instanceof ApiError && error.estado === 403)) {
            mostrar('error', mensajeApuntes(error));
          }
        },
      },
    );
  };

  return (
    <form onSubmit={alEnviar} className='flex flex-col gap-4' noValidate>
      {/* Selector de origen (Tabs) visible solo si Google Drive está configurado */}
      {isGoogleDriveConfigurado && (
        <div className='flex border-b border-tiza-borde gap-2' role='tablist' aria-label='Origen del apunte'>
          <button
            type='button'
            role='tab'
            aria-selected={origen === 'local'}
            onClick={() => cambiarOrigen('local')}
            className={`pb-2 px-3 text-sm font-semibold border-b-2 transition-colors ${
              origen === 'local'
                ? 'border-tiza-bosque text-tiza-bosque'
                : 'border-transparent text-tiza-pizarra/60 hover:text-tiza-bosque'
            }`}
          >
            Desde mi equipo
          </button>
          <button
            type='button'
            role='tab'
            aria-selected={origen === 'drive'}
            onClick={() => cambiarOrigen('drive')}
            className={`pb-2 px-3 text-sm font-semibold border-b-2 transition-colors ${
              origen === 'drive'
                ? 'border-tiza-bosque text-tiza-bosque'
                : 'border-transparent text-tiza-pizarra/60 hover:text-tiza-bosque'
            }`}
          >
            Google Drive
          </button>
        </div>
      )}

      {/* Control de archivo según origen */}
      <div className='flex flex-col gap-1.5'>
        <label htmlFor='apunte-archivo' className='text-sm font-semibold text-tiza-bosque'>
          Archivo PDF (hasta 10 MB)
        </label>

        {origen === 'local' ? (
          <input
            ref={entradaArchivo}
            id='apunte-archivo'
            type='file'
            accept='application/pdf,.pdf'
            onChange={(e) => alElegir(e.target.files?.[0] ?? null)}
            aria-invalid={errorArchivo ? true : undefined}
            aria-describedby={errorArchivo ? 'apunte-archivo-error' : undefined}
            className='w-full rounded-xl border border-tiza-borde bg-white p-3 text-tiza-bosque'
          />
        ) : (
          <div className='flex flex-col gap-2'>
            {archivo ? (
              <div className='flex items-center justify-between rounded-xl border border-tiza-borde bg-white p-3 text-tiza-bosque'>
                <div className='flex items-center gap-2 overflow-hidden'>
                  <FileText className='h-5 w-5 shrink-0 text-tiza-bosque' aria-hidden='true' />
                  <div className='truncate text-sm'>
                    <span className='font-semibold'>{archivo.name}</span>
                    <span className='ml-2 text-xs text-tiza-pizarra/60'>
                      ({formatearTamano(archivo.size)})
                    </span>
                  </div>
                </div>
                <button
                  type='button'
                  onClick={() => alElegir(null)}
                  className='ml-2 shrink-0 text-xs font-semibold text-tiza-coral hover:underline'
                >
                  Cambiar archivo
                </button>
              </div>
            ) : (
              <button
                type='button'
                onClick={handleElegirDeDrive}
                disabled={isDescargando}
                className='flex w-full items-center justify-center gap-2 rounded-xl border border-dashed border-tiza-borde bg-white p-4 text-sm font-semibold text-tiza-bosque transition-colors hover:border-tiza-bosque hover:bg-tiza-menta/30 disabled:opacity-50'
              >
                <Cloud className='h-5 w-5 text-tiza-bosque' aria-hidden='true' />
                {isDescargando ? 'Descargando de Drive…' : 'Elegir archivo desde Google Drive'}
              </button>
            )}
          </div>
        )}

        {errorArchivo && (
          <p id='apunte-archivo-error' role='alert' className='text-sm text-tiza-coral'>
            {errorArchivo}
          </p>
        )}
      </div>

      <Campo
        etiqueta='Título (opcional)'
        value={titulo}
        maxLength={TITULO_MAXIMO}
        placeholder='Si lo dejás vacío, se usa el nombre del archivo'
        onChange={(e) => setTitulo(e.target.value)}
      />

      <div>
        <Boton
          type='submit'
          cargando={subir.isPending}
          disabled={!archivo || !!errorArchivo || isDescargando}
        >
          <Upload className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
          Subir apunte
        </Boton>
      </div>
    </form>
  );
}
