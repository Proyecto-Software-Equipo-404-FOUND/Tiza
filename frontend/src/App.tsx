import { RouterProvider } from 'react-router-dom';
import { Proveedores } from './app/Proveedores';
import { crearRouter } from './app/rutas';
import { SesionProvider } from './features/auth/SesionContext';

const router = crearRouter();

export default function App() {
  return (
    <Proveedores>
      <SesionProvider>
        <RouterProvider router={router} />
      </SesionProvider>
    </Proveedores>
  );
}
