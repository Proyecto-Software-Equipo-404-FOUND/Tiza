import { useState, type ReactNode } from 'react';
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiError } from '@/shared/api/cliente';
import { mensajeDeError } from '@/shared/api/errores';
import { AvisosProvider, useAviso } from '@/shared/components/Aviso';

// Errores que no se arreglan reintentando: datos inválidos, sin sesión, sin permiso, inexistente
// o conflicto. Solo se reintenta ante fallas de red o del servidor.
const SIN_REINTENTO = [400, 401, 403, 404, 409];

function ProveedorQuery({ children }: { children: ReactNode }) {
  const { mostrar } = useAviso();

  const [cliente] = useState(() => {
    // Un 403 se avisa igual en toda la app. El 401 no pasa por acá: `pedir` limpia el token y
    // lleva al login. El resto de los errores los muestra cada pantalla junto a lo que falló.
    const avisarSinPermiso = (error: unknown) => {
      if (error instanceof ApiError && error.estado === 403) mostrar('error', mensajeDeError(error));
    };
    return new QueryClient({
      queryCache: new QueryCache({ onError: avisarSinPermiso }),
      mutationCache: new MutationCache({ onError: avisarSinPermiso }),
      defaultOptions: {
        queries: {
          retry: (intentos, error) =>
            !(error instanceof ApiError && SIN_REINTENTO.includes(error.estado)) && intentos < 2,
        },
      },
    });
  });

  return <QueryClientProvider client={cliente}>{children}</QueryClientProvider>;
}

export function Proveedores({ children }: { children: ReactNode }) {
  return (
    <AvisosProvider>
      <ProveedorQuery>{children}</ProveedorQuery>
    </AvisosProvider>
  );
}
