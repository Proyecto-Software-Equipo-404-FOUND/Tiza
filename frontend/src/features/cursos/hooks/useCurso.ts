import { useQuery } from '@tanstack/react-query';
import { obtenerCurso } from '../api/cursos';

export const useCurso = (id: string) =>
  useQuery({ queryKey: ['cursos', id], queryFn: () => obtenerCurso(id) });