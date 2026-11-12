import { useQuery } from '@tanstack/react-query';
import { useSesion } from '@/features/auth/SesionContext';
import { listarAlumnos } from '../api/usuarios';
import { nombreCompleto, nombreDeReemplazo } from '../formato';

/**
 * Nombres de los alumnos, que viven en Identidad (las entregas solo traen el id).
 * Si falla o falta alguien, `nombreDe` devuelve un texto de reemplazo y la pantalla sigue funcionando.
 */
export function useNombresAlumnos() {
  const { usuario } = useSesion();
  const institucionId = usuario?.institucionId ?? '';
  const consulta = useQuery({
    queryKey: ['usuarios', institucionId, 'Alumno'],
    queryFn: listarAlumnos,
    enabled: institucionId !== '',
    staleTime: 60_000,
  });

  const porId = new Map((consulta.data ?? []).map((alumno) => [alumno.id, nombreCompleto(alumno)]));
  return {
    nombreDe: (alumnoId: string) => porId.get(alumnoId) ?? nombreDeReemplazo(alumnoId),
    cargando: consulta.isPending,
    fallo: consulta.isError,
  };
}
