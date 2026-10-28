| Servicio | Método | Ruta | Respuestas contrato | Respuestas Swagger | Resultado |
|---|---|---|---|---|---|
| cursos | GET | `/api/v1/cursos/{cursoId}/cohortes` | 200, 401, 404 | 200, 404 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | POST | `/api/v1/cursos/{cursoId}/cohortes` | 201, 400, 401, 404, 409 | 201, 400, 404, 409 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | PUT | `/api/v1/cursos/{cursoId}/cohortes/{id}` | 200, 400, 401, 404, 409 | 200, 400, 404, 409 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | DELETE | `/api/v1/cursos/{cursoId}/cohortes/{id}` | 204, 400, 401, 404 | 204, 400, 404 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | GET | `/api/v1/cohortes/{id}/validacion` | 200, 401, 404 | 200, 404 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | GET | `/api/v1/cursos/{id}` | 200, 401, 404 | 200, 404 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | PUT | `/api/v1/cursos/{id}` | 200, 400, 401, 404, 409 | 200, 400, 404, 409 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | DELETE | `/api/v1/cursos/{id}` | 204, 401, 404 | 204, 400, 404 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | GET | `/api/v1/cursos` | 200, 401 | 200 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | POST | `/api/v1/cursos` | 201, 400, 401, 409 | 201, 400, 409 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | GET | `/api/v1/cohortes/{cohorteId}/matriculas/{id}` | 200, 401, 404 | 200, 404 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| cursos | POST | `/api/v1/cohortes/{cohorteId}/matriculas` | 201, 400, 401, 404, 409 | 201, 400, 404, 409 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| identidad | POST | `/api/auth/login` | — | 200, 400, 401, 415, 429, 500 | SIN CONTRATO VERSIONADO |
| identidad | POST | `/api/auth/register` | — | 201, 400, 401, 403, 409, 415, 500 | SIN CONTRATO VERSIONADO |
| identidad | POST | `/api/auth/refresh` | — | 200, 400, 401, 415, 429, 500 | SIN CONTRATO VERSIONADO |
| identidad | POST | `/api/auth/logout` | — | 204, 400, 415, 429, 500 | SIN CONTRATO VERSIONADO |
| identidad | GET | `/api/auth/administrador` | — | 200, 401, 403, 500 | SIN CONTRATO VERSIONADO |
| identidad | GET | `/api/auth/docente` | — | 200, 401, 403, 500 | SIN CONTRATO VERSIONADO |
| identidad | GET | `/api/auth/alumno` | — | 200, 401, 403, 500 | SIN CONTRATO VERSIONADO |
| identidad | POST | `/api/plataforma/auth/login` | — | 200, 400, 401, 404, 415, 429, 500 | SIN CONTRATO VERSIONADO |
| identidad | GET | `/api/plataforma/instituciones` | — | 200, 400, 401, 403, 404, 500 | SIN CONTRATO VERSIONADO |
| identidad | POST | `/api/plataforma/instituciones` | — | 201, 400, 401, 403, 404, 409, 415, 500 | SIN CONTRATO VERSIONADO |
| identidad | POST | `/api/plataforma/invitaciones/activar` | — | 204, 400, 401, 404, 415, 429, 500 | SIN CONTRATO VERSIONADO |
| identidad | GET | `/WeatherForecast` | — | 200 | SIN CONTRATO VERSIONADO |
| ia | POST | `/api/v1/Correcciones` | 200, 400, 500 | 200, 400, 500 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| ia | GET | `/api/v1/Correcciones/health` | 200 | 200 | RUTA PRESENTE; REQUIERE REVISION SEMANTICA |
| evaluaciones | GET | `/api/v1/evaluaciones` | — | 200 | SIN CONTRATO VERSIONADO |
| evaluaciones | POST | `/api/v1/evaluaciones` | — | 200 | SIN CONTRATO VERSIONADO |
| evaluaciones | GET | `/api/v1/evaluaciones/{id}` | — | 200 | SIN CONTRATO VERSIONADO |
| evaluaciones | PUT | `/api/v1/evaluaciones/{id}` | — | 200 | SIN CONTRATO VERSIONADO |
| evaluaciones | DELETE | `/api/v1/evaluaciones/{id}` | — | 200 | SIN CONTRATO VERSIONADO |
| evaluaciones | POST | `/api/v1/evaluaciones/{id}/publicar` | — | 200 | SIN CONTRATO VERSIONADO |
| evaluaciones | GET | `/WeatherForecast` | — | 200 | SIN CONTRATO VERSIONADO |
| sala-en-vivo | POST | `/api/v1/salas` | — | 200 | SIN CONTRATO VERSIONADO |
| sala-en-vivo | GET | `/WeatherForecast` | — | 200 | SIN CONTRATO VERSIONADO |
