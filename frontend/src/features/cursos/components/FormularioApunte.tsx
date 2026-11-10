import { useRef, useState, type FormEvent } from 'react';
import { Upload } from 'lucide-react';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { useAviso } from '@/shared/components/Aviso';
import { mensajeApuntes } from '../mensajesApuntes';
import { TITULO_MAXIMO, validarArchivo } from '../apuntes';
import { useSubirMaterial } from '../hooks/useSubirMaterial';

type Props = { cursoId: string };

export function FormularioApunte({ cursoId }: Props) {
  const subir = useSubirMaterial(cursoId);
  const { mostrar } = useAviso();
  const [archivo, setArchivo] = useState<File | null>(null);
  const [titulo, setTitulo] = useState('');
  const [errorArchivo, setErrorArchivo] = useState<string | undefined>();
  const entradaArchivo = useRef<HTMLInputElement>(null);

  const alElegir = (elegido: File | null) => {
    setArchivo(elegido);
    setErrorArchivo(elegido ? (validarArchivo(elegido) ?? undefined) : undefined);
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
        onError: (error) => mostrar('error', mensajeApuntes(error)),
      },
    );
  };

  return (
    <form onSubmit={alEnviar} className='flex flex-col gap-4' noValidate>
      <div className='flex flex-col gap-1.5'>
        <label htmlFor='apunte-archivo' className='text-sm font-semibold text-tiza-bosque'>
          Archivo PDF (hasta 10 MB)
        </label>
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
        <Boton type='submit' cargando={subir.isPending} disabled={!archivo || !!errorArchivo}>
          <Upload className='mr-1.5 inline h-4 w-4' aria-hidden='true' />
          Subir apunte
        </Boton>
      </div>
    </form>
  );
}