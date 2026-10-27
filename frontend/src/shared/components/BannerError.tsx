import { Boton } from './Boton';

type Props = {
  mensaje?: string;
  onReintentar?: () => void;
};

export function BannerError({ mensaje = 'Algo salió mal. Probá de nuevo.', onReintentar }: Props) {
  return (
    <div role='alert' className='flex flex-wrap items-center justify-between gap-3 rounded-xl border border-tiza-coral bg-tiza-coral/10 p-4 text-tiza-bosque'>
      <p>{mensaje}</p>
      {onReintentar && (
        <Boton variante='secundario' onClick={onReintentar}>
          Reintentar
        </Boton>
      )}
    </div>
  );
}
