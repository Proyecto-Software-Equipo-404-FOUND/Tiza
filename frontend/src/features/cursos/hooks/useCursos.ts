import { useQuery } from '@tanstack/react-query';
import { listarCursos } from '../api/cursos';

export const useCursos = () => useQuery({ queryKey: ['cursos'], queryFn: listarCursos });