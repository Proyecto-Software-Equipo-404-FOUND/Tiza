import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { BannerError } from '@/shared/components/BannerError';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { useCrearCohorte } from '../hooks/useCrearCohorte';

const esquema = z.object({
  nombre: z.string().trim().min(1, 'Ingresá un nombre').max(100, 'Máximo 100 caracteres'),
});

export function FormularioCohorte({ cursoId }: { cursoId: string }) {
  const [nombre, setNombre] = useState('');
  const [errorCampo, setErrorCampo] = useState<string | undefined>();
  const crear = useCrearCohorte(cursoId);
  const cliente = useQueryClient();
  const { mostrar } = useAviso();

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();

    // Se valida antes de enviar para no hacer pedidos inútiles
    const resultado = esquema.safeParse({ nombre });
    if (!resultado.success) {
      setErrorCampo(resultado.error.issues[0].message);
      return;
    }

    setErrorCampo(undefined);
    crear.mutate(resultado.data, {
      onSuccess: () => {
        setNombre('');
        mostrar('exito', 'Cohorte creada.');
      },
    });
  };

  // Ante un 409 la lista puede estar desactualizada: se ofrece recargarla
  const recargar = () => {
    crear.reset();
    cliente.invalidateQueries({ queryKey: ['cohortes', cursoId] });
  };

  const hayConflicto = crear.isError && crear.error instanceof ApiError && crear.error.estado === 409;

  return (
    <form onSubmit={enviar} noValidate className='flex flex-col gap-3'>
      <Campo
        etiqueta='Nombre de la cohorte'
        value={nombre}
        onChange={(evento) => setNombre(evento.target.value)}
        error={errorCampo}
        disabled={crear.isPending}
      />

      {crear.isError && (
        <BannerError
          mensaje={mensajeDeError(crear.error)}
          onReintentar={hayConflicto ? recargar : undefined}
        />
      )}

      <Boton type='submit' cargando={crear.isPending} className='self-start'>
        Crear cohorte
      </Boton>
    </form>
  );
}