import { createBrowserRouter, type RouteObject } from 'react-router-dom';
import { PaginaIngresoInvitado } from '@/features/auth/pages/PaginaIngresoInvitado';
import { PaginaLogin } from '@/features/auth/pages/PaginaLogin';
import { PaginaApuntes } from '@/features/cursos/pages/PaginaApuntes';
import { PaginaCohortes } from '@/features/cursos/pages/PaginaCohortes';
import { PaginaCursos } from '@/features/cursos/pages/PaginaCursos';
import { PaginaEditarEvaluacion } from '@/features/evaluaciones/pages/PaginaEditarEvaluacion';
import { PaginaEvaluacion } from '@/features/evaluaciones/pages/PaginaEvaluacion';
import { PaginaEvaluaciones } from '@/features/evaluaciones/pages/PaginaEvaluaciones';
import { PaginaNuevaEvaluacion } from '@/features/evaluaciones/pages/PaginaNuevaEvaluacion';
import { PaginaMetricas } from '@/features/metricas/PaginaMetricas';
import { PaginaMisExamenes } from '@/features/mis-examenes/PaginaMisExamenes';
import { PaginaMatriculas } from '@/features/matriculas/PaginaMatriculas';
import { PaginaEntrega } from '@/features/revision-notas/pages/PaginaEntrega';
import { PaginaResultados } from '@/features/revision-notas/pages/PaginaResultados';
import { PaginaUsuarios } from '@/features/usuarios/PaginaUsuarios';
import { PaginaSalaDocente } from '@/features/sala-docente/PaginaSalaDocente';
import { Layout } from './Layout';
import { PaginaErrorRuta } from './PaginaErrorRuta';
import { PaginaInicio } from './PaginaInicio';
import { PaginaNoAutorizada } from './PaginaNoAutorizada';
import { PaginaNoEncontrada } from './PaginaNoEncontrada';
import { RutaProtegida } from './RutaProtegida';
import { RutaPublica } from './RutaPublica';
import { SalaAlumnoConSesion } from './SalaAlumnoConSesion';

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
      // Ingreso del invitado (PIN + nombre, sin cuenta). Sin RutaPublica: al entrar, la redirección a Inicio
      // pelearía con la navegación a /sala y se perdería el PIN que viaja en el state.
      { path: '/invitado', element: <PaginaIngresoInvitado /> },
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
                  { path: '/cursos/:cursoId/apuntes', element: <PaginaApuntes /> },
                  { path: '/cohortes/:cohorteId/matriculas', element: <PaginaMatriculas /> },
                  { path: '/evaluaciones', element: <PaginaEvaluaciones /> },
                  { path: '/evaluaciones/nueva', element: <PaginaNuevaEvaluacion /> },
                  { path: '/evaluaciones/:evaluacionId', element: <PaginaEvaluacion /> },
                  { path: '/evaluaciones/:evaluacionId/editar', element: <PaginaEditarEvaluacion /> },
                  { path: '/evaluaciones/:evaluacionId/resultados', element: <PaginaResultados /> },
                  { path: '/evaluaciones/:evaluacionId/resultados/:entregaId', element: <PaginaEntrega /> },
                  { path: '/evaluaciones/:evaluacionId/metricas', element: <PaginaMetricas /> },
                ],
              },
              {
                // Solo administrador: alta de alumnos y docentes (POST /api/auth/register)
                element: <RutaProtegida roles={['administrador']} />,
                children: [{ path: '/usuarios', element: <PaginaUsuarios /> }],
              },
              {
                // Solo el docente conduce la sala: el hub rechaza al administrador (contrato sala-hub-v1, 4.2)
                element: <RutaProtegida roles={['docente']} />,
                children: [{ path: '/evaluaciones/:evaluacionId/sala', element: <PaginaSalaDocente /> }],
              },
              {
                // Alumno e invitado: el invitado solo ve la sala (contrato sala-hub-v1, 13.1)
                element: <RutaProtegida roles={['alumno', 'invitado']} />,
                children: [{ path: '/sala', element: <SalaAlumnoConSesion /> }],
              },
              {
                // Solo alumnos: «Mis exámenes» no aplica a invitados
                element: <RutaProtegida roles={['alumno']} />,
                children: [{ path: '/mis-examenes', element: <PaginaMisExamenes /> }],
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
