import { Campo } from '@/shared/components/Campo';
import { formatearNumero } from '../formato';

type Props = {
  numero: number;
  maximo: number;
  sugerida: number | null | undefined;
  valor: string;
  error: string | null;
  deshabilitado: boolean;
  onCambiar: (valor: string) => void;
};

// Campo donde el docente confirma o corrige el puntaje de una pregunta abierta
export function CampoPuntajeFinal({ numero, maximo, sugerida, valor, error, deshabilitado, onCambiar }: Props) {
  return (
    <div className='mt-4 max-w-xs'>
      <Campo
        etiqueta={`Nota final de la pregunta ${numero} (máximo ${formatearNumero(maximo)})`}
        inputMode='decimal'
        autoComplete='off'
        value={valor}
        disabled={deshabilitado}
        error={error ?? undefined}
        onChange={(evento) => onCambiar(evento.target.value)}
      />
      {sugerida != null && !deshabilitado && (
        <p className='mt-1 text-xs text-tiza-bosque/70'>La IA sugirió {formatearNumero(sugerida)}.</p>
      )}
    </div>
  );
}
