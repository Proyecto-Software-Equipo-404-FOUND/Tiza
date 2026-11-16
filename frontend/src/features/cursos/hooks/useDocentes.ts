import { useQuery } from '@tanstack/react-query';
import { listarDocentes } from '../api/docentes';

export const useDocentes = () => useQuery({ queryKey: ['docentes'], queryFn: listarDocentes });
