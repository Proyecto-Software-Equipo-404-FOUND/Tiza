import { http, HttpResponse } from 'msw';
import { usuarios } from './datos';
import { crearTokenFalso } from './token';

const CUATRO_HORAS_S = 4 * 60 * 60;

// PINs de la demo del alumno (mocks/conexionDemo.ts) con los que el invitado puede entrar.
// Para probar los errores: 666666 no admite invitados, 777777 no se puede verificar, 222222 ya finalizó;
// cualquier otro PIN no existe.
const PINES_CON_SALA = [123456, 111111, 444444, 555555];

// Los errores de negocio llevan su código en `type` (contrato identidad-v1, POST /api/auth/invitado)
const problema = (estado: number, codigo: string, titulo: string) =>
  HttpResponse.json({ title: titulo, status: estado, type: codigo }, { status: estado });

export const handlersInvitado = [
  // Ruta real por el gateway: el transform la entrega a Identidad como /api/auth/invitado
  http.post('*/api/v1/identidad/auth/invitado', async ({ request }) => {
    const cuerpo = (await request.json()) as { pin?: number; nombre?: string };
    const nombre = (cuerpo.nombre ?? '').trim();
    const pin = Number(cuerpo.pin);

    if (nombre.length < 2 || nombre.length > 60) {
      return problema(400, 'NOMBRE_INVALIDO', 'El nombre tiene que tener entre 2 y 60 caracteres.');
    }
    if (pin === 666666) {
      return problema(403, 'SALA_SIN_INVITADOS', 'Esta sala no admite invitados. Pedile al docente que te habilite.');
    }
    if (pin === 777777) {
      return problema(503, 'SALA_NO_DISPONIBLE', 'No pudimos verificar la sala. Probá de nuevo.');
    }
    if (pin === 222222) {
      return problema(409, 'SALA_FINALIZADA', 'La sala ya finalizó.');
    }
    if (!PINES_CON_SALA.includes(pin)) {
      return problema(404, 'SALA_NO_ENCONTRADA', 'Sala no encontrada.');
    }

    const salaId = crypto.randomUUID();
    const token = crearTokenFalso(
      { id: crypto.randomUUID(), nombre, rol: 'invitado', institucionId: usuarios[1].institucionId },
      { exp: Math.floor(Date.now() / 1000) + CUATRO_HORAS_S, salaId },
    );
    return HttpResponse.json({
      accessToken: token,
      expiraEnUtc: new Date(Date.now() + CUATRO_HORAS_S * 1000).toISOString(),
      salaId,
      nombre,
    });
  }),
];