import { crearConexionFalsa } from './conexionFalsa';
import type { EstadoConexion } from './tipos';

describe('conexionFalsa', () => {
  it('entrega un evento simulado al manejador', () => {
    const { conexion, simular } = crearConexionFalsa();
    const manejador = vi.fn();
    conexion.escuchar('alumnoIngreso', manejador);

    // Payload del contrato 6.1
    const ingreso = {
      salaId: '11111111-1111-4111-8111-111111111111',
      alumnoId: 'aaaaaaaa-0000-4000-8000-000000000001',
      alias: 'Alumno 1',
      totalConectados: 1,
      ingresoUtc: '2026-11-05T22:00:00Z',
    };
    simular('alumnoIngreso', ingreso);

    expect(manejador).toHaveBeenCalledWith(ingreso);
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
