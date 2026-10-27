import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, EyeOff, Lock, Mail } from 'lucide-react';
import { z } from 'zod';
import { ApiError, guardarTokenRenovacion } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { IconoLapiz, Logo } from '@/shared/components/Logo';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { iniciarSesion } from '../api/auth';
import { rutaInicial } from '../rutaInicial';
import { useSesion } from '../SesionContext';

const esquema = z.object({
  correo: z.string().min(1, 'Ingresá tu correo').email('Ingresá un correo válido'),
  contrasena: z.string().min(1, 'Ingresá tu contraseña'),
});

type Errores = { correo?: string; contrasena?: string };

export function PaginaLogin() {
  const navegar = useNavigate();
  const { entrar } = useSesion();
  const [correo, setCorreo] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [errores, setErrores] = useState<Errores>({});
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);
  const [verContrasena, setVerContrasena] = useState(false);

  const enviar = async (evento: FormEvent) => {
    evento.preventDefault();
    // Se limpia antes de validar: si no, el error de un intento anterior
    // convive con los errores de validación del intento actual
    setErrorGeneral(null);

    const resultado = esquema.safeParse({ correo, contrasena });
    if (!resultado.success) {
      const campos = resultado.error.flatten().fieldErrors;
      setErrores({ correo: campos.correo?.[0], contrasena: campos.contrasena?.[0] });
      return;
    }

    setErrores({});
    setCargando(true);
    try {
      const { accessToken, refreshToken } = await iniciarSesion(
        resultado.data.correo,
        resultado.data.contrasena,
      );
      const usuario = entrar(accessToken);
      // Se guarda después de entrar: guardarToken(null) lo limpia, pero entrar no lo hace
      if (refreshToken) guardarTokenRenovacion(refreshToken);
      navegar(rutaInicial(usuario.rol), { replace: true });
    } catch (error) {
      // Un 401 al iniciar sesión no dice cuál de los dos datos falló
      setErrorGeneral(
        error instanceof ApiError && error.estado === 401
          ? 'Correo o contraseña incorrectos'
          : mensajeDeError(error),
      );
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className='min-h-screen bg-tiza-fondo'>
      <div className='mx-auto flex min-h-screen max-w-6xl flex-col gap-8 px-6 py-8'>
        <Logo tamano='grande' />
        <div className='grid flex-1 items-center gap-10 pb-8 lg:grid-cols-2'>
          <img
            src='/ilustracion-login.png'
            alt=''
            aria-hidden='true'
            className='hidden w-full max-w-xl justify-self-center lg:block'
          />
          <div className='mx-auto w-full max-w-lg'>
            <Tarjeta
              titulo='Ingresar a Tiza'
              subtitulo='Tu espacio para aprender y evaluar'
              icono={<IconoLapiz className='h-9 w-9' />}
            >
              <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
                <Campo
                  etiqueta='Correo'
                  type='email'
                  autoComplete='email'
                  placeholder='nombre@ejemplo.com'
                  icono={<Mail className='h-5 w-5' aria-hidden='true' />}
                  value={correo}
                  onChange={(e) => {
                    setCorreo(e.target.value);
                    setErrorGeneral(null);
                  }}
                  error={errores.correo}
                  disabled={cargando}
                />
                <Campo
                  etiqueta='Contraseña'
                  type={verContrasena ? 'text' : 'password'}
                  autoComplete='current-password'
                  placeholder='Ingresá tu contraseña'
                  icono={<Lock className='h-5 w-5' aria-hidden='true' />}
                  accion={
                    <button
                      type='button'
                      onClick={() => setVerContrasena((v) => !v)}
                      aria-label={verContrasena ? 'Ocultar la contraseña' : 'Mostrar la contraseña'}
                      className='rounded-md p-1 text-tiza-bosque/70 hover:text-tiza-bosque'
                    >
                      {verContrasena ? (
                        <EyeOff className='h-5 w-5' aria-hidden='true' />
                      ) : (
                        <Eye className='h-5 w-5' aria-hidden='true' />
                      )}
                    </button>
                  }
                  value={contrasena}
                  onChange={(e) => {
                    setContrasena(e.target.value);
                    setErrorGeneral(null);
                  }}
                  error={errores.contrasena}
                  disabled={cargando}
                />
                {errorGeneral && <BannerError mensaje={errorGeneral} />}
                <Boton type='submit' cargando={cargando}>
                  Ingresar
                </Boton>
              </form>
            </Tarjeta>
          </div>
        </div>
      </div>
    </div>
  );
}
