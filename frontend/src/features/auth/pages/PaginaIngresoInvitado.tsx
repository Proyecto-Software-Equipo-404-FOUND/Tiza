import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { Hash, User } from 'lucide-react';
import { z } from 'zod';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { IconoTiza, Logo } from '@/shared/components/Logo';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { ingresarComoInvitado } from '../api/invitado';
import { MENSAJE_ACCESO_VENCIDO, mensajeIngresoInvitado } from '../mensajesInvitado';
import { useSesion } from '../SesionContext';

// Contrato identidad-v1 (POST /api/auth/invitado): pin entero de 100000 a 999999, nombre de 2 a 60 caracteres
const esquema = z.object({
  pin: z.string().regex(/^[1-9]\d{5}$/, 'Ingresá el PIN de 6 dígitos'),
  nombre: z
    .string()
    .trim()
    .min(2, 'Ingresá tu nombre y apellido (al menos 2 caracteres)')
    .max(60, 'El nombre puede tener hasta 60 caracteres'),
});

type Errores = { pin?: string; nombre?: string };

export function PaginaIngresoInvitado() {
  const navegar = useNavigate();
  const { entrar } = useSesion();
  // Si el token del invitado venció (dura 4 h y no se renueva), se vuelve acá con ?vencido=1
  const [parametros] = useSearchParams();
  const accesoVencido = parametros.get('vencido') === '1';
  const [pin, setPin] = useState('');
  const [nombre, setNombre] = useState('');
  const [errores, setErrores] = useState<Errores>({});
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null);
  const [cargando, setCargando] = useState(false);

  const enviar = async (evento: FormEvent) => {
    evento.preventDefault();
    setErrorGeneral(null);

    const resultado = esquema.safeParse({ pin, nombre });
    if (!resultado.success) {
      const campos = resultado.error.flatten().fieldErrors;
      setErrores({ pin: campos.pin?.[0], nombre: campos.nombre?.[0] });
      return;
    }

    setErrores({});
    setCargando(true);
    try {
      const sesion = await ingresarComoInvitado(Number(resultado.data.pin), resultado.data.nombre);
      entrar(sesion.accessToken);
      navegar('/sala', { replace: true, state: { pin: resultado.data.pin } });
    } catch (error) {
      setErrorGeneral(mensajeIngresoInvitado(error));
    } finally {
      setCargando(false);
    }
  };

  return (
    <div className='min-h-screen bg-tiza-fondo'>
      <div className='mx-auto flex min-h-screen max-w-lg flex-col gap-8 px-6 py-8'>
        <Logo tamano='grande' />
        <Tarjeta
          titulo='Entrar como invitado'
          subtitulo='Ingresá el PIN que te dio tu docente y tu nombre. No necesitás una cuenta.'
          icono={<IconoTiza className='h-9 w-9' />}
        >
          <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
            <Campo
              etiqueta='PIN de la sala'
              type='text'
              inputMode='numeric'
              autoComplete='off'
              maxLength={6}
              placeholder='123456'
              icono={<Hash className='h-5 w-5' aria-hidden='true' />}
              value={pin}
              onChange={(e) => {
                setPin(e.target.value.replace(/\D/g, ''));
                setErrorGeneral(null);
              }}
              error={errores.pin}
              disabled={cargando}
            />
            <Campo
              etiqueta='Nombre y apellido'
              type='text'
              autoComplete='name'
              maxLength={60}
              placeholder='Tu nombre y apellido'
              icono={<User className='h-5 w-5' aria-hidden='true' />}
              value={nombre}
              onChange={(e) => {
                setNombre(e.target.value);
                setErrorGeneral(null);
              }}
              error={errores.nombre}
              disabled={cargando}
            />
            {accesoVencido && !errorGeneral && <BannerError mensaje={MENSAJE_ACCESO_VENCIDO} />}
            {errorGeneral && <BannerError mensaje={errorGeneral} />}
            <Boton type='submit' cargando={cargando}>
              Entrar a la sala
            </Boton>
          </form>
          <p className='mt-5 text-center text-sm'>
            <Link to='/login' className='font-bold text-tiza-bosque underline'>
              Tengo una cuenta de Tiza
            </Link>
          </p>
        </Tarjeta>
      </div>
    </div>
  );
}
