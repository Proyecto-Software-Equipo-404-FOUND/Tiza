import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { BannerError } from '@/shared/components/BannerError';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { useCrearCurso } from '../hooks/useCrearCurso';
import { AREAS_SUGERIDAS } from '../semillas';

const esquema = z.object({
  nombre: z.string().trim().min(1, 'Ingresá un nombre').max(100, 'Máximo 100 caracteres'),
  area: z.string().trim().min(1, 'Ingresá un área').max(50, 'Máximo 50 caracteres'),
});

type ErroresCampo = { nombre?: string; area?: string };

export function FormularioCurso() {
  const [nombre, setNombre] = useState('');
  const [area, setArea] = useState('');
  const [errores, setErrores] = useState<ErroresCampo>({});
  const crear = useCrearCurso();
  const cliente = useQueryClient();
  const { mostrar } = useAviso();

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();

    // Se valida antes de enviar para no hacer pedidos inútiles
    const resultado = esquema.safeParse({ nombre, area });
    if (!resultado.success) {
      const nuevos: ErroresCampo = {};
      for (const problema of resultado.error.issues) {
        const campo = problema.path[0] as keyof ErroresCampo;
        nuevos[campo] ??= problema.message;
      }
      setErrores(nuevos);
      return;
    }

    setErrores({});
    crear.mutate(resultado.data, {
      onSuccess: () => {
        setNombre('');
        setArea('');
        mostrar('exito', 'Curso creado.');
      },
    });
  };

  // Ante un 409 la lista puede estar desactualizada: se ofrece recargarla
  const recargar = () => {
    crear.reset();
    cliente.invalidateQueries({ queryKey: ['cursos'] });
  };

  const hayConflicto = crear.isError && crear.error instanceof ApiError && crear.error.estado === 409;

  return (
    <form onSubmit={enviar} noValidate className='flex flex-col gap-3'>
      <Campo
        etiqueta='Nombre del curso'
        value={nombre}
        onChange={(evento) => setNombre(evento.target.value)}
        error={errores.nombre}
        disabled={crear.isPending}
      />

      <Campo
        etiqueta='Área'
        value={area}
        onChange={(evento) => setArea(evento.target.value)}
        error={errores.area}
        disabled={crear.isPending}
        list='areas-sugeridas'
      />
      <datalist id='areas-sugeridas'>
        {AREAS_SUGERIDAS.map((sugerida) => (
          <option key={sugerida} value={sugerida} />
        ))}
      </datalist>

      {crear.isError && (
        <BannerError
          mensaje={mensajeDeError(crear.error)}
          onReintentar={hayConflicto ? recargar : undefined}
        />
      )}

      <Boton type='submit' cargando={crear.isPending} className='self-start'>
        Crear curso
      </Boton>
    </form>
  );
}