import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { CalendarDays, UserRound, Users } from 'lucide-react';
import { z } from 'zod';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { CampoSelect } from '@/shared/components/CampoSelect';
import { BannerError } from '@/shared/components/BannerError';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import type { Cohorte } from '@/shared/api/tipos';
import { useActualizarCohorte } from '../hooks/useActualizarCohorte';
import { DOCENTES_SEMILLA } from '../semillas';

const esquema = z.object({
  nombre: z.string().trim().min(1, 'Ingresá un nombre').max(100, 'Máximo 100 caracteres'),
  periodo: z.string().trim().min(1, 'Ingresá un período').max(30, 'Máximo 30 caracteres'),
  docenteId: z.string().min(1, 'Elegí un docente'),
});

type ErroresCampo = { nombre?: string; periodo?: string; docenteId?: string };

type Props = { cursoId: string; cohorte: Cohorte; onTerminar: () => void };

export function FormularioEditarCohorte({ cursoId, cohorte, onTerminar }: Props) {
  const [nombre, setNombre] = useState(cohorte.nombre);
  const [periodo, setPeriodo] = useState(cohorte.periodo);
  const [docenteId, setDocenteId] = useState(cohorte.docenteId);
  const [errores, setErrores] = useState<ErroresCampo>({});
  const actualizar = useActualizarCohorte(cursoId, cohorte.id);
  const cliente = useQueryClient();
  const { mostrar } = useAviso();

  // Si el docente actual no está en la lista provisoria, se conserva como opción para no perderlo
  const docentes = DOCENTES_SEMILLA.some((d) => d.id === cohorte.docenteId)
    ? DOCENTES_SEMILLA
    : [...DOCENTES_SEMILLA, { id: cohorte.docenteId, nombre: 'Docente actual' }];

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();

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
    actualizar.mutate(resultado.data, {
      onSuccess: () => {
        mostrar('exito', 'Cohorte actualizada.');
        onTerminar();
      },
    });
  };

  // Ante un 409 los datos que se ven están desactualizados: se vuelven a pedir y se sale de la edición
  const recargar = () => {
    actualizar.reset();
    cliente.invalidateQueries({ queryKey: ['cohortes', cursoId] });
    onTerminar();
  };

  const hayConflicto =
    actualizar.isError && actualizar.error instanceof ApiError && actualizar.error.estado === 409;

  return (
    <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
      <Campo
        etiqueta='Nombre de la cohorte'
        icono={<Users className='h-5 w-5' aria-hidden='true' />}
        value={nombre}
        onChange={(evento) => setNombre(evento.target.value)}
        error={errores.nombre}
        disabled={actualizar.isPending}
      />

      <Campo
        etiqueta='Período'
        icono={<CalendarDays className='h-5 w-5' aria-hidden='true' />}
        placeholder='2026-1C'
        value={periodo}
        onChange={(evento) => setPeriodo(evento.target.value)}
        error={errores.periodo}
        disabled={actualizar.isPending}
      />

      <CampoSelect
        etiqueta='Docente'
        id='docente-cohorte-edicion'
        icono={<UserRound className='h-5 w-5' aria-hidden='true' />}
        value={docenteId}
        onChange={(evento) => setDocenteId(evento.target.value)}
        error={errores.docenteId}
        disabled={actualizar.isPending}
      >
        {docentes.map((docente) => (
          <option key={docente.id} value={docente.id}>
            {docente.nombre}
          </option>
        ))}
      </CampoSelect>

      {actualizar.isError && (
        <BannerError
          mensaje={mensajeDeError(actualizar.error)}
          onReintentar={hayConflicto ? recargar : undefined}
          textoBoton='Recargar'
        />
      )}

      <div className='flex flex-wrap gap-3'>
        <Boton type='submit' cargando={actualizar.isPending}>
          Guardar cambios
        </Boton>
        <Boton type='button' variante='secundario' onClick={onTerminar} disabled={actualizar.isPending}>
          Cancelar
        </Boton>
      </div>
    </form>
  );
}
