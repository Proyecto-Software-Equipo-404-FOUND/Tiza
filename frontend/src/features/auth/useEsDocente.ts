import { useSesion } from './SesionContext';

// La sala en vivo la conduce solo el docente: el hub rechaza al administrador (contrato sala-hub-v1, 4.2).
export function useEsDocente(): boolean {
  return useSesion().usuario?.rol === 'docente';
}
