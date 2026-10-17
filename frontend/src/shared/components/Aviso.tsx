import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';

type TipoAviso = 'exito' | 'error';
type ItemAviso = { id: number; tipo: TipoAviso; texto: string };

type ContextoAvisos = {
  mostrar: (tipo: TipoAviso, texto: string) => void;
};

const AvisosContext = createContext<ContextoAvisos | null>(null);

const DURACION_MS = 5000;

export function AvisosProvider({ children }: { children: ReactNode }) {
  const [avisos, setAvisos] = useState<ItemAviso[]>([]);

  const mostrar = useCallback((tipo: TipoAviso, texto: string) => {
    const id = Date.now() + Math.random();
    setAvisos((actuales) => [...actuales, { id, tipo, texto }]);
    setTimeout(() => setAvisos((actuales) => actuales.filter((a) => a.id !== id)), DURACION_MS);
  }, []);

  return (
    <AvisosContext.Provider value={{ mostrar }}>
      {children}
      <div className='fixed bottom-4 right-4 z-50 flex flex-col gap-2' aria-live='polite'>
        {avisos.map((aviso) => (
          <div
            key={aviso.id}
            role={aviso.tipo === 'error' ? 'alert' : 'status'}
            className={`rounded-md px-4 py-3 font-bold text-tiza-pizarra shadow-md ${
              aviso.tipo === 'exito' ? 'bg-tiza-lima' : 'bg-tiza-coral'
            }`}
          >
            {aviso.texto}
          </div>
        ))}
      </div>
    </AvisosContext.Provider>
  );
}

export function useAviso() {
  const contexto = useContext(AvisosContext);
  if (!contexto) throw new Error('useAviso debe usarse dentro de AvisosProvider');
  return contexto;
}
