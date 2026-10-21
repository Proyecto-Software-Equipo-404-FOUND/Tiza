import { crearConexionFalsa } from './conexionFalsa';
import type { EstadoConexion } from './tipos';

describe('conexionFalsa', () => {
  it('entrega un evento simulado al manejador', () => {
    const { conexion, simular } = crearConexionFalsa();
    const manejador = vi.fn();
    conexion.escuchar('alumnoIngreso', manejador);

    simular('alumnoIngreso', 'Alumno 1 A');

    expect(manejador).toHaveBeenCalledWith('Alumno 1 A');
  });

  it('no falla si nadie escucha el evento', () => {
    const { simular } = crearConexionFalsa();
    expect(() => simular('eventoSinOyentes')).not.toThrow();
  });

  it('deja de avisar cuando se llama a la función que devolvió alCambiarEstado', async () => {
    const { conexion, simularEstado } = crearConexionFalsa();
    const estados: EstadoConexion[] = [];
    const dejarDeEscuchar = conexion.alCambiarEstado((estado) => estados.push(estado));

    simularEstado('reconectando');
    dejarDeEscuchar();
    simularEstado('desconectado');

    expect(estados).toEqual(['reconectando']);
  });

  it('informa los cambios de estado', async () => {
    const { conexion, simularEstado } = crearConexionFalsa();
    const estados: EstadoConexion[] = [];
    conexion.alCambiarEstado((estado) => estados.push(estado));

    await conexion.iniciar();
    simularEstado('reconectando');

    expect(estados).toEqual(['conectado', 'reconectando']);
  });
});
