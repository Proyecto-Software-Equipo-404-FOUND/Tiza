import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
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

  const enviar = async (evento: FormEvent) => {
    evento.preventDefault();
    const resultado = esquema.safeParse({ correo, contrasena });
    if (!resultado.success) {
      const campos = resultado.error.flatten().fieldErrors;
      setErrores({ correo: campos.correo?.[0], contrasena: campos.contrasena?.[0] });
      return;
    }

    setErrores({});
    setErrorGeneral(null);
    setCargando(true);
    try {
      const { accessToken } = await iniciarSesion(resultado.data.correo, resultado.data.contrasena);
      const usuario = entrar(accessToken);
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
    <div className='mx-auto flex min-h-screen max-w-md flex-col justify-center gap-4 p-4'>
      <Tarjeta titulo='Ingresar a Tiza'>
        <form onSubmit={enviar} noValidate className='flex flex-col gap-4'>
          <Campo
            etiqueta='Correo'
            type='email'
            autoComplete='email'
            value={correo}
            onChange={(e) => setCorreo(e.target.value)}
            error={errores.correo}
            disabled={cargando}
          />
          <Campo
            etiqueta='Contraseña'
            type='password'
            autoComplete='current-password'
            value={contrasena}
            onChange={(e) => setContrasena(e.target.value)}
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
  );
}
