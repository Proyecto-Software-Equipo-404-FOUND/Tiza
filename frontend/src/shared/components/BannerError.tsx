import { Boton } from './Boton';

type Props = {
  mensaje?: string;
  onReintentar?: () => void;
  /** Texto del botón de la acción (por defecto «Reintentar») */
  textoBoton?: string;
};

export function BannerError({ mensaje = 'Algo salió mal. Probá de nuevo.', onReintentar, textoBoton = 'Reintentar' }: Props) {
  return (
    <div role='alert' className='flex flex-wrap items-center justify-between gap-3 rounded-xl border border-tiza-coral bg-tiza-coral/10 p-4 text-tiza-bosque'>
      <p>{mensaje}</p>
      {onReintentar && (
        <Boton type='button' variante='secundario' onClick={onReintentar}>
          {textoBoton}
        </Boton>
      )}
    </div>
  );
}
