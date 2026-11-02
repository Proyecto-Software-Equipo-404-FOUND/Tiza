import { createBrowserRouter, type RouteObject } from 'react-router-dom';
import { PaginaLogin } from '@/features/auth/pages/PaginaLogin';
import { PaginaCohortes } from '@/features/cursos/pages/PaginaCohortes';
import { PaginaCursos } from '@/features/cursos/pages/PaginaCursos';
import { PaginaEvaluacion } from '@/features/evaluaciones/pages/PaginaEvaluacion';
import { PaginaEvaluaciones } from '@/features/evaluaciones/pages/PaginaEvaluaciones';
import { PaginaMatriculas } from '@/features/matriculas/PaginaMatriculas';
import { PaginaSalaAlumno } from '@/features/sala-alumno/PaginaSalaAlumno';
import { Layout } from './Layout';
import { PaginaErrorRuta } from './PaginaErrorRuta';
import { PaginaInicio } from './PaginaInicio';
import { PaginaNoAutorizada } from './PaginaNoAutorizada';
import { PaginaNoEncontrada } from './PaginaNoEncontrada';
import { RutaProtegida } from './RutaProtegida';
import { RutaPublica } from './RutaPublica';

// Ocultar pantallas acá mejora la experiencia, pero no es seguridad: el backend rechaza con 403.
export const rutas: RouteObject[] = [
  {
    errorElement: <PaginaErrorRuta />,
    children: [
      {
        path: '/login',
        element: (
          <RutaPublica>
            <PaginaLogin />
          </RutaPublica>
        ),
      },
      {
        // Cualquier persona con sesión
        element: <RutaProtegida />,
        children: [
          {
            element: <Layout />,
            children: [
              { path: '/', element: <PaginaInicio /> },
              { path: '/no-autorizado', element: <PaginaNoAutorizada /> },
              {
                // Solo docente y administrador
                element: <RutaProtegida roles={['docente', 'administrador']} />,
                children: [
                  { path: '/cursos', element: <PaginaCursos /> },
                  { path: '/cursos/:cursoId/cohortes', element: <PaginaCohortes /> },
                  { path: '/cohortes/:cohorteId/matriculas', element: <PaginaMatriculas /> },
                  { path: '/evaluaciones', element: <PaginaEvaluaciones /> },
                  { path: '/evaluaciones/:evaluacionId', element: <PaginaEvaluacion /> },
                ],
              },
              {
                // Solo alumnos
                element: <RutaProtegida roles={['alumno']} />,
                children: [{ path: '/sala', element: <PaginaSalaAlumno /> }],
              },
            ],
          },
        ],
      },
      { path: '*', element: <PaginaNoEncontrada /> },
    ],
  },
];

export const crearRouter = () => createBrowserRouter(rutas);
