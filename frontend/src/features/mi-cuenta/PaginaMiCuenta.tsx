import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import { Lock } from 'lucide-react';
import { cambiarContrasena } from '@/features/auth/api/auth';
import { useSesion } from '@/features/auth/SesionContext';
import { mensajeDeError } from '@/shared/api/errores';
import { useAviso } from '@/shared/components/Aviso';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { Tarjeta } from '@/shared/components/Tarjeta';

// Mismas reglas que el servidor (S3-ID-03): 8 caracteres o más, 72 bytes o menos, no solo espacios
const MINIMA = 8;
const MAXIMA_BYTES = 72;

type Errores = { actual?: string; nueva?: string; confirmacion?: string };

export function validarCambio(actual: string, nueva: string, confirmacion: string): Errores {
  const errores: Errores = {};
  if (!actual) errores.actual = 'Ingresá tu contraseña actual';
  if (nueva.trim().length === 0 || nueva.length < MINIMA)
    errores.nueva = `La contraseña nueva tiene que tener al menos ${MINIMA} caracteres`;
  else if (new TextEncoder().encode(nueva).length > MAXIMA_BYTES)
    errores.nueva = 'La contraseña nueva es demasiado larga';
  else if (actual && nueva === actual) errores.nueva = 'La contraseña nueva tiene que ser distinta de la actual';
  if (!errores.nueva && confirmacion !== nueva) errores.confirmacion = 'Las contraseñas no coinciden';
  return errores;
}

export function PaginaMiCuenta() {
  const { usuario, salir } = useSesion();
  const clienteQuery = useQueryClient();
  const navegar = useNavigate();
  const { mostrar } = useAviso();
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [errores, setErrores] = useState<Errores>({});
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);

  const enviar = async (evento: FormEvent) => {
    evento.preventDefault();
    setErrorGeneral(null);
    const encontrados = validarCambio(actual, nueva, confirmacion);
    setErrores(encontrados);
    if (Object.keys(encontrados).length > 0) return;

    setCargando(true);
    try {
      await cambiarContrasena(actual, nueva);
      // El servidor cerró las demás sesiones: se sale también de esta y se vuelve a ingresar con la nueva
      salir();
      clienteQuery.clear();
      mostrar('exito', 'Cambiaste tu contraseña. Ingresá de nuevo con la nueva.');
      navegar('/login', { replace: true });
    } catch (error) {
      setErrorGeneral(mensajeDeError(error));
      setCargando(false);
    }
  };

  return (
    <main className='mx-auto flex max-w-2xl flex-col gap-6 p-6'>
      <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Mi cuenta</h1>

      <Tarjeta titulo='Tus datos'>
        <dl className='grid gap-2 sm:grid-cols-[8rem_1fr]'>
          <dt className='text-sm font-semibold text-tiza-bosque/70'>Nombre</dt>
          <dd>{usuario?.nombre}</dd>
          <dt className='text-sm font-semibold text-tiza-bosque/70'>Rol</dt>
          <dd className='capitalize'>{usuario?.rol}</dd>
        </dl>
      </Tarjeta>

      <Tarjeta
        titulo='Cambiar contraseña'
        subtitulo='Al cambiarla se cierran tus sesiones abiertas y tenés que volver a ingresar.'
      >
        <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
          <Campo
            etiqueta='Contraseña actual'
            type='password'
            autoComplete='current-password'
            icono={<Lock className='h-5 w-5' aria-hidden='true' />}
            value={actual}
            onChange={(e) => setActual(e.target.value)}
            error={errores.actual}
            disabled={cargando}
          />
          <Campo
            etiqueta='Contraseña nueva'
            type='password'
            autoComplete='new-password'
            placeholder={`Al menos ${MINIMA} caracteres`}
            icono={<Lock className='h-5 w-5' aria-hidden='true' />}
            value={nueva}
            onChange={(e) => setNueva(e.target.value)}
            error={errores.nueva}
            disabled={cargando}
          />
          <Campo
            etiqueta='Repetí la contraseña nueva'
            type='password'
            autoComplete='new-password'
            icono={<Lock className='h-5 w-5' aria-hidden='true' />}
            value={confirmacion}
            onChange={(e) => setConfirmacion(e.target.value)}
            error={errores.confirmacion}
            disabled={cargando}
          />
          {errorGeneral && <BannerError mensaje={errorGeneral} />}
          <Boton type='submit' cargando={cargando}>
            Cambiar contraseña
          </Boton>
        </form>
      </Tarjeta>
    </main>
  );
}
