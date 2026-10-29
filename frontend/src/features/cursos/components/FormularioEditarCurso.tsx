import { useState, type FormEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { BookOpen, Layers } from 'lucide-react';
import { z } from 'zod';
import { Boton } from '@/shared/components/Boton';
import { Campo } from '@/shared/components/Campo';
import { BannerError } from '@/shared/components/BannerError';
import { useAviso } from '@/shared/components/Aviso';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import type { Curso } from '@/shared/api/tipos';
import { useActualizarCurso } from '../hooks/useActualizarCurso';
import { AREAS_SUGERIDAS } from '../semillas';

const esquema = z.object({
  nombre: z.string().trim().min(1, 'Ingresá un nombre').max(100, 'Máximo 100 caracteres'),
  area: z.string().trim().min(1, 'Ingresá un área').max(50, 'Máximo 50 caracteres'),
});

type ErroresCampo = { nombre?: string; area?: string };

type Props = { curso: Curso; onTerminar: () => void };

export function FormularioEditarCurso({ curso, onTerminar }: Props) {
  const [nombre, setNombre] = useState(curso.nombre);
  const [area, setArea] = useState(curso.area);
  const [errores, setErrores] = useState<ErroresCampo>({});
  const actualizar = useActualizarCurso(curso.id);
  const cliente = useQueryClient();
  const { mostrar } = useAviso();

  const enviar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();

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
    actualizar.mutate(resultado.data, {
      onSuccess: () => {
        mostrar('exito', 'Curso actualizado.');
        onTerminar();
      },
    });
  };

  // Ante un 409 los datos que se ven están desactualizados: se vuelven a pedir y se sale de la edición
  const recargar = () => {
    actualizar.reset();
    cliente.invalidateQueries({ queryKey: ['cursos'] });
    onTerminar();
  };

  const hayConflicto =
    actualizar.isError && actualizar.error instanceof ApiError && actualizar.error.estado === 409;

  return (
    <form onSubmit={enviar} noValidate className='flex flex-col gap-5'>
      <Campo
        etiqueta='Nombre del curso'
        icono={<BookOpen className='h-5 w-5' aria-hidden='true' />}
        value={nombre}
        onChange={(evento) => setNombre(evento.target.value)}
        error={errores.nombre}
        disabled={actualizar.isPending}
      />

      <Campo
        etiqueta='Área'
        icono={<Layers className='h-5 w-5' aria-hidden='true' />}
        value={area}
        onChange={(evento) => setArea(evento.target.value)}
        error={errores.area}
        disabled={actualizar.isPending}
        list='areas-sugeridas-edicion'
      />
      <datalist id='areas-sugeridas-edicion'>
        {AREAS_SUGERIDAS.map((sugerida) => (
          <option key={sugerida} value={sugerida} />
        ))}
      </datalist>

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
