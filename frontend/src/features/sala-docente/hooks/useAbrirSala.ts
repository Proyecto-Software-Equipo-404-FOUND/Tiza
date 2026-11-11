import { useMutation } from '@tanstack/react-query';
import { abrirSala, type DatosAbrirSala } from '../api/salas';

// No hay listado de salas que invalidar: el resultado (id y PIN) lo usa directo la pantalla
export function useAbrirSala() {
  return useMutation({
    mutationFn: (datos: DatosAbrirSala) => abrirSala(datos),
  });
}