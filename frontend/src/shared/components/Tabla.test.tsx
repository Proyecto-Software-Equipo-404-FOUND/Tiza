import { render, screen } from '@testing-library/react';
import { Tabla } from './Tabla';

type Fila = { id: string; nombre: string };
const columnas = [{ titulo: 'Nombre', celda: (f: Fila) => f.nombre }];

describe('Tabla', () => {
  it('muestra las filas', () => {
    render(<Tabla columnas={columnas} filas={[{ id: '1', nombre: 'Programación I' }]} clave={(f) => f.id} />);
    expect(screen.getByText('Programación I')).toBeInTheDocument();
  });

  it('muestra el estado vacío', () => {
    render(<Tabla columnas={columnas} filas={[]} clave={(f: Fila) => f.id} textoVacio='Todavía no hay cursos.' />);
    expect(screen.getByText('Todavía no hay cursos.')).toBeInTheDocument();
  });
});
