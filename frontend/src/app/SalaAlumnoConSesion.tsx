import { useLocation } from 'react-router-dom';
import { useSesion } from '@/features/auth/SesionContext';
import { PaginaSalaAlumno } from '@/features/sala-alumno/PaginaSalaAlumno';

// El invitado llega desde su pantalla de ingreso con el PIN que ya escribió (state del router): se le precarga
function pinDelRouter(state: unknown): string | undefined {
  const pin = (state as { pin?: unknown } | null)?.pin;
  return typeof pin === 'string' && /^\d{6}$/.test(pin) ? pin : undefined;
}

export function SalaAlumnoConSesion() {
  const { usuario } = useSesion();
  const { state } = useLocation();
  const esInvitado = usuario?.rol === 'invitado';
  return <PaginaSalaAlumno esInvitado={esInvitado} pinInicial={esInvitado ? pinDelRouter(state) : undefined} />;
}