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
import { DOCENTES_SEMILLA } from '../semillas';

const esquema = z.object({
  nombre: z.string().trim().min(1, 'Ingresá un nombre').max(100, 'Máximo 100 caracteres'),
  periodo: z.string().trim().min(1, 'Ingresá un período').max(30, 'Máximo 30 caracteres'),
  docenteId: z.string().min(1, 'Elegí un docente'),
});

type ErroresCampo = { nombre?: string; periodo?: string; docenteId?: string };

export function FormularioCohorte({ cursoId }: { cursoId: string }) {
  const [nombre, setNombre] = useState('');
  const [periodo, setPeriodo] = useState('');
  const [docenteId, setDocenteId] = useState(DOCENTES_SEMILLA[0].id);
  const [errores, setErrores] = useState<ErroresCampo>({});
  const crear = useCrearCohorte(cursoId);
  const cliente = useQueryClient();
  const { mostrar } = useAviso();

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();

    // Se valida antes de enviar para no hacer pedidos inútiles
    const resultado = esquema.safeParse({ nombre, periodo, docenteId });
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
        setPeriodo('');
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
        error={errores.nombre}
        disabled={crear.isPending}
      />

      <Campo
        etiqueta='Período'
        placeholder='2026-1C'
        value={periodo}
        onChange={(evento) => setPeriodo(evento.target.value)}
        error={errores.periodo}
        disabled={crear.isPending}
      />

      <div className='flex flex-col gap-1'>
        <label htmlFor='docente-cohorte' className='font-bold text-tiza-bosque'>
          Docente
        </label>
        <select
          id='docente-cohorte'
          value={docenteId}
          onChange={(evento) => setDocenteId(evento.target.value)}
          disabled={crear.isPending}
          className='rounded-xl border border-tiza-borde bg-white px-3 py-3 text-tiza-bosque disabled:cursor-not-allowed disabled:opacity-50'
        >
          {DOCENTES_SEMILLA.map((docente) => (
            <option key={docente.id} value={docente.id}>
              {docente.nombre}
            </option>
          ))}
        </select>
        {errores.docenteId && (
          <p role='alert' className='text-sm text-tiza-coral'>
            {errores.docenteId}
          </p>
        )}
      </div>

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