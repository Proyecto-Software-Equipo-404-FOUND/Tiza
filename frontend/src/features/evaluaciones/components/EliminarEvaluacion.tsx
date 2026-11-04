import { useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { useAviso } from '@/shared/components/Aviso';
import { BannerError } from '@/shared/components/BannerError';
import { Boton } from '@/shared/components/Boton';
import { Tarjeta } from '@/shared/components/Tarjeta';
import { useEliminarEvaluacion } from '../hooks/useEliminarEvaluacion';

export function EliminarEvaluacion({ evaluacionId }: { evaluacionId: string }) {
  const [confirmando, setConfirmando] = useState(false);
  const eliminar = useEliminarEvaluacion(evaluacionId);
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const { mostrar } = useAviso();

  const hayConflicto = eliminar.error instanceof ApiError && eliminar.error.estado === 409;

  // Ante un 409 (por ejemplo, ya la publicaron) se vuelve a pedir y se muestra el estado actual
  const recargar = () => {
    eliminar.reset();
    setConfirmando(false);
    cliente.invalidateQueries({ queryKey: ['evaluaciones'] });
  };

  const confirmar = () => {
    setConfirmando(false);
    eliminar.mutate(undefined, {
      onSuccess: () => {
        mostrar('exito', 'Borrador eliminado.');
        navegar('/evaluaciones');
      },
    });
  };

  return (
    <Tarjeta titulo='Eliminar borrador' subtitulo='Se borra y no se puede recuperar. Solo se pueden borrar borradores.'>
      <div className='flex flex-col gap-4'>
        {Boolean(eliminar.error) && (
          <BannerError
            mensaje={mensajeDeError(eliminar.error)}
            onReintentar={hayConflicto ? recargar : undefined}
            textoBoton='Recargar'
          />
        )}

        {confirmando ? (
          <div role='group' aria-label='Confirmar la eliminación del borrador' className='flex flex-wrap items-center gap-3'>
            <span className='font-semibold text-tiza-bosque'>¿Eliminar este borrador?</span>
            <Boton type='button' variante='peligro' onClick={confirmar}>
              Sí, eliminar
            </Boton>
            <Boton type='button' variante='secundario' onClick={() => setConfirmando(false)}>
              Cancelar
            </Boton>
          </div>
        ) : (
          <Boton
            type='button'
            variante='secundario'
            className='self-start !text-tiza-coral'
            cargando={eliminar.isPending}
            onClick={() => setConfirmando(true)}
          >
            <Trash2 className='mr-2 inline h-4 w-4' aria-hidden='true' />
            Eliminar borrador
          </Boton>
        )}
      </div>
    </Tarjeta>
  );
}
