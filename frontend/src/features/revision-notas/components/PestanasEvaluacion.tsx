import { Link } from 'react-router-dom';

type Props = {
  evaluacionId: string;
  actual: 'detalle' | 'resultados';
};

const base = 'rounded-full px-4 py-2 text-sm font-bold';

// Pestañas «Detalle» y «Resultados» de una evaluación. Son rutas distintas, por eso son enlaces.
export function PestanasEvaluacion({ evaluacionId, actual }: Props) {
  const estilo = (activa: boolean) =>
    `${base} ${activa ? 'bg-tiza-bosque text-white' : 'bg-tiza-menta text-tiza-bosque hover:bg-tiza-menta/70'}`;
  return (
    <nav aria-label='Secciones de la evaluación' className='flex gap-2'>
      <Link
        to={`/evaluaciones/${evaluacionId}`}
        className={estilo(actual === 'detalle')}
        aria-current={actual === 'detalle' ? 'page' : undefined}
      >
        Detalle
      </Link>
      <Link
        to={`/evaluaciones/${evaluacionId}/resultados`}
        className={estilo(actual === 'resultados')}
        aria-current={actual === 'resultados' ? 'page' : undefined}
      >
        Resultados
      </Link>
    </nav>
  );
}
