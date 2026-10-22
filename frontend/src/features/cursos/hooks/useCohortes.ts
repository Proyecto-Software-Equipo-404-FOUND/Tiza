import { useQuery } from '@tanstack/react-query';
import { listarCohortes } from '../api/cohortes';

export const useCohortes = (cursoId: string) =>
  useQuery({ queryKey: ['cohortes', cursoId], queryFn: () => listarCohortes(cursoId) });