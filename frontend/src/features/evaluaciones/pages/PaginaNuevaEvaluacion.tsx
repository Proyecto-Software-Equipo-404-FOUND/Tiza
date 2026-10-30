import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAviso } from '@/shared/components/Aviso';
import { borradorVacio } from '../borrador';
import { FormularioEvaluacion } from '../components/FormularioEvaluacion';
import { useCrearEvaluacion } from '../hooks/useCrearEvaluacion';

export function PaginaNuevaEvaluacion() {
  const crear = useCrearEvaluacion();
  const navegar = useNavigate();
  const { mostrar } = useAviso();
  const [inicial] = useState(borradorVacio);

  return (
    <main className='mx-auto flex max-w-4xl flex-col gap-6 p-6'>
      <Link to='/evaluaciones' className='text-sm font-semibold text-tiza-pasto hover:underline'>
        ← Volver a evaluaciones
      </Link>

      <header>
        <h1 className='font-heading text-4xl font-extrabold text-tiza-bosque'>Nueva evaluación</h1>
        <p className='mt-1 text-tiza-bosque/70'>Se guarda como borrador. Después la podés editar y publicar.</p>
      </header>

      <FormularioEvaluacion
        inicial={inicial}
        textoEnviar='Guardar borrador'
        enviando={crear.isPending}
        error={crear.error}
        onEnviar={(datos) =>
          crear.mutate(datos, {
            onSuccess: (evaluacion) => {
              mostrar('exito', 'Borrador creado.');
              navegar(`/evaluaciones/${evaluacion.id}`);
            },
          })
        }
        onCancelar={() => navegar('/evaluaciones')}
      />
    </main>
  );
}
