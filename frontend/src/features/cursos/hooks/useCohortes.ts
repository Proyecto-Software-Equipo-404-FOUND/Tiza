import { useQuery } from '@tanstack/react-query';
import { listarCohortes } from '../api/cohortes';

// Con cursoId vacío no se pide nada (por ejemplo, un selector de curso todavía sin elegir)
export const useCohortes = (cursoId: string) =>
  useQuery({ queryKey: ['cohortes', cursoId], queryFn: () => listarCohortes(cursoId), enabled: cursoId !== '' });
