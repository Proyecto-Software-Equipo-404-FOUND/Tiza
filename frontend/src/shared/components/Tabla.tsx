import type { ReactNode } from 'react';

export type Columna<T> = {
  titulo: string;
  celda: (fila: T) => ReactNode;
};

type Props<T> = {
  columnas: Columna<T>[];
  filas: T[];
  clave: (fila: T) => string;
  textoVacio?: string;
};

export function Tabla<T>({ columnas, filas, clave, textoVacio = 'No hay datos para mostrar.' }: Props<T>) {
  return (
    <div className='overflow-x-auto'>
      <table className='w-full border-collapse text-left'>
        <thead>
          <tr className='bg-tiza-pizarra text-tiza-fondo'>
            {columnas.map((columna) => (
              <th key={columna.titulo} scope='col' className='px-3 py-2 font-heading'>
                {columna.titulo}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {filas.length === 0 ? (
            <tr>
              <td colSpan={columnas.length} className='px-3 py-6 text-center text-tiza-pizarra/70'>
                {textoVacio}
              </td>
            </tr>
          ) : (
            filas.map((fila) => (
              <tr key={clave(fila)} className='border-b border-tiza-pizarra/10'>
                {columnas.map((columna) => (
                  <td key={columna.titulo} className='px-3 py-2'>
                    {columna.celda(fila)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
