# QA-06 — Revisión de contratos

Responsable: Candela Luna. Fecha: 23/10/2026. Base revisada: `origin/main` 853f3f6. Estado: **PARCIAL — revisión técnica realizada; PENDIENTE DE VALIDACIÓN POR PRODUCT OWNER** (Nicolás Monasterio).

## Fuentes y alcance

Se leyó el backlog local, fila QA-06 (Sprint 1, En Curso, vencimiento 15/10) y QA-04 (Pendiente, vencimiento 21/10, dependencia CU-06). Se revisaron los dos OpenAPI congelados, JSON Schema, controladores, DTO, handlers, middleware, filtros de EF y pruebas existentes. Los documentos externos enlazados en el backlog no pudieron recuperarse: el conector de Drive no estaba disponible. Referencias pendientes: documento QA-04 `1YzsQDDvWe-zrDJjizb67Jhm6CoA4HyZDcN-PHkx0FdY` y carpeta QA-06 `1liYLFPP5AlGAvTe4OLp1M8Xm5fj4t4pL`. No se les atribuyen requisitos no leídos.

El [inventario](evidencias/qa06/endpoints.md) cubre las **35 operaciones** encontradas en cinco Swagger; tres son WeatherForecast de plantilla. Se capturaron sin bases reales ni llamadas LLM. Los [detalles estructurales](evidencias/qa06/semantic-review.json) incluyen, para cada operación, parámetros, tipos, required, formatos, restricciones, cuerpo y respuestas. Los 14 métodos/rutas con contrato congelado están presentes (12 Cursos, 2 IA); esto no implica conformidad semántica. Identidad tiene Swagger enriquecido por `IdentitySecurityOperationFilter`, pero no snapshot congelado previo. Evaluaciones y Sala sólo tienen Swagger generado.

## Matriz por endpoint

Prefijos de implementación: `services/{servicio}/src/PEI.{Servicio}.Api/Controllers/`. Las entradas y salidas exactas, incluidos campos obligatorios, están en los cinco JSON capturados y `semantic-review.json`; la tabla identifica la regla y el resultado de la revisión estática. HTTP no ejecutado aquí se indica expresamente; QA-04 aporta la evidencia HTTP de Cursos en su propia rama.

| Requisito / método y ruta | Contrato / implementación | Entradas, salidas y validaciones | Prueba / resultado |
|---|---|---|---|
| GET /api/v1/cursos | cursos-v1 / CursosController.GetCursos | area,busqueda opcionales; pagina=1,tamanoPagina=10; ResultadoPaginadoDTO<CursoDto> | Inventario + lectura: tenant JWT; QA04-01 pendiente de ejecución en rama QA-04 |
| GET /api/v1/cursos/{id} | cursos-v1 / GetById | UUID; CursoDto o 404 | QA04-02 previsto; H01 |
| POST /api/v1/cursos | cursos-v1 / CrearCurso | nombre/area string; dominio no vacíos, máximos 150/50; tenant del contexto; 201 CursoDto+Location | Revisión estática; restricciones de dominio no completas en Swagger; H02 |
| PUT /api/v1/cursos/{id} | cursos-v1 / ActualizarCurso | UUID+nombre/area; 200 CursoDto; 400 dominio,404,409 | QA04-04 previsto; H01/H02 |
| DELETE /api/v1/cursos/{id} | cursos-v1 / DesactivarCurso | UUID, baja lógica; 204/404 y 400 anotado no congelado | Revisión estática; H01/H05 |
| GET /api/v1/cursos/{cursoId}/cohortes | cursos-v1 / CohortesController.GetCohortes | UUID padre, lista CohorteDto; padre ajeno/inexistente404 | QA04-03 previsto; H01 |
| POST /api/v1/cursos/{cursoId}/cohortes | cursos-v1 / CrearCohorte | nombre100,periodo30 no vacíos,docenteId UUID no vacío; 201 | Revisión estática; H02/H07/H08 |
| PUT /api/v1/cursos/{cursoId}/cohortes/{id} | cursos-v1 / ActualizarCohorte | dos UUID+DTO anterior; 200/400/404/409 | QA04-05 previsto; H01/H02/H08 |
| DELETE /api/v1/cursos/{cursoId}/cohortes/{id} | cursos-v1 / DesactivarCohorte | UUID padre/hijo, baja lógica;204/400/404 | Revisión estática; H01/H02 |
| GET /api/v1/cohortes/{id}/validacion | cursos-v1 / ValidarCohorte | UUID, metadatos cohorte o404 | Revisión estática: filtro tenant; H01 |
| POST /api/v1/cohortes/{cohorteId}/matriculas | cursos-v1 / MatriculacionesController.MatricularAlumno | alumnoId UUID; padre activo, duplicados409;201 MatriculacionDto | Revisión estática; H02/H08 |
| GET /api/v1/cohortes/{cohorteId}/matriculas/{id} | cursos-v1 / GetById | UUID padre/hijo; comprueba coincidencia del padre;200/404 | Revisión estática; H01 |
| POST /api/auth/login | Swagger / AuthController.Login | email requerido válido≤150,password requerido y límite bytes; SessionResponse con tokens/expiraciones;200/400/401/429 | 169 tests Identidad PASS (incluyen OpenAPI/HTTP); login real en QA-04 previsto |
| POST /api/auth/register | Swagger / Register | InstitutionAdmin; tenant tomado del JWT, rechaza JSON extra;201 id/message;400/401/403/409 | Tests Identidad PASS; no credenciales en evidencia |
| POST /api/auth/refresh | Swagger / Refresh | refreshToken requerido; rota sesión;200 SessionResponse/400/401/429 | Tests Identidad PASS |
| POST /api/auth/logout | Swagger / Logout | refreshToken; anónimo, idempotente;204/400/429 | Tests Identidad PASS |
| GET /api/auth/administrador | Swagger / SoloAdministrador | política InstitutionAdmin;200 mensaje/401/403 | Tests Identidad PASS |
| GET /api/auth/docente | Swagger / SoloDocente | rol Docente;200 mensaje/401/403 | Tests Identidad PASS |
| GET /api/auth/alumno | Swagger / SoloAlumno | rol Alumno;200 mensaje/401/403 | Tests Identidad PASS |
| POST /api/plataforma/auth/login | Swagger / PlataformaController.Login | LoginCommand; token global separado; flag apagado404 | Revisión + tests Identidad; plataforma no habilitada en captura |
| GET /api/plataforma/instituciones | Swagger / Listar | política PlatformAdmin, pagina int;200 listado;401/403,flag404 | Revisión + tests Identidad |
| POST /api/plataforma/instituciones | Swagger / Crear | CrearInstitucionCommand, validaciones handler;201 resultado;400/409 | Revisión + tests Identidad |
| POST /api/plataforma/invitaciones/activar | Swagger / Activar | ActivarAdministradorCommand; token invitación y password;204/400/401/404 | Revisión + tests Identidad; no envío de correos |
| POST /api/v1/Correcciones | ia-v1 / CorreccionesController.Corregir | respuestaTexto≤10000,preguntaEnunciado≤2000,rubricaJson requeridos;puntajeMaximo decimal0.1..100; GUID opcionales;200 CorregirRespuestaResponse/400/500 | 21 tests IA PASS, sin LLM real; H01/H03 |
| GET /api/v1/Correcciones/health | ia-v1 / Health | sin entrada;200 status/service/timestamp | Revisión: body real anónimo sin esquema Swagger; H05 |
| GET /api/v1/evaluaciones | Swagger / EvaluacionesController.Listar | X-Institucion-Id UUID;200 listado/400 sin tenant | Revisión estática; H03/H05; HTTP con datos no ejecutado |
| POST /api/v1/evaluaciones | Swagger / Crear | título/tipo/preguntasJson; schema real;201 DTO+Location/400 | 12 nuevas pruebas de validador PASS; H03/H05 |
| GET /api/v1/evaluaciones/{id} | Swagger / ObtenerPorId | UUID+header;200 DTO/400/404 | Revisión estática; H03/H05 |
| PUT /api/v1/evaluaciones/{id} | Swagger / Actualizar | UUID+header+EvaluacionRequest;200/400/404/409 | Revisión estática; H03/H05 |
| DELETE /api/v1/evaluaciones/{id} | Swagger / Eliminar | UUID+header;204/400/404/409 | Revisión estática; H03/H05 |
| POST /api/v1/evaluaciones/{id}/publicar | Swagger / Publicar | versionEsperada int,cohorteId UUID;200/400/404/409 | Revisión: CursosClientProvisorio; integración real pendiente por alcance |
| POST /api/v1/salas | Swagger / SalasController.CrearSala | CrearSalaCommand: institución/evaluación UUID,capacidad int;201 id/PIN/vencimiento/capacidad | Revisión estática; H03/H05/H07 |
| GET /WeatherForecast (Identidad) | Swagger / WeatherForecastController | plantilla200 WeatherForecast[] | Sin requisito funcional; pendiente limpieza por alcance |
| GET /WeatherForecast (Evaluaciones) | Swagger / WeatherForecastController | plantilla200 WeatherForecast[] | Sin requisito funcional; pendiente limpieza por alcance |
| GET /WeatherForecast (Sala) | Swagger / WeatherForecastController | plantilla200 WeatherForecast[] | Sin requisito funcional; pendiente limpieza por alcance |

Todos los endpoints Cursos requieren autenticación, no políticas de rol. Su filtro EF es cerrado sin InstitucionId. Identidad valida firma, claims, usuario e institución activos, roles y responde ProblemDetails. IA se declara interna y no tiene JWT. Evaluaciones/Sala no autentican el tenant: no se extiende el resultado QA-04 a estos servicios. `GET` lista Cursos sin tenant devuelve vacío; detalle ajeno devuelve404, sin revelar nombre/área. Crear sin tenant falla por `DomainException : ArgumentException` (400). Estos últimos comportamientos requieren confirmación HTTP QA-04.

## Hallazgos

| ID | Severidad | Ubicación / evidencia | Resolución y estado |
|---|---|---|---|
| QA06-H01 | MEDIO | Cursos: NotFound devuelve `{error}`, challenge JWT sin ProblemDetails; congelado declara ProblemDetails. Swagger omite401. IA validación automática usa application/problem+json frente application/json congelado. | ABIERTO: unificar errores/anotaciones y congelar luego de acuerdo. No se alteró producción para ocultar diferencia. |
| QA06-H02 | ALTO | Tres controladores Cursos sólo `[Authorize]`; ningún Roles/Policy. Autenticación no establece autorización de escritura por rol. | ABIERTO: PO y responsable Cursos deben fijar matriz Docente/Administrador/Alumno y agregar pruebas negativas. No se afirma que Alumno haya sido ensayado ni que QA04-10 cubra todos los roles. |
| QA06-H03 | CRÍTICO si se expone como API multi-tenant | EvaluacionesController usa header X-Institucion-Id libre; SalasController acepta InstitucionId del body; no JWT en ambos Program. IA es interna por comentario y acepta institución opcional. | PENDIENTE POR ALCANCE/INTEGRACIÓN: responsables de seguridad y servicios deben autenticar contexto o demostrar frontera de red confiable. Bloquea certificar seguridad global, no es un fallo demostrado del aislamiento Cursos. No se inventa compromiso de entrega de sprint posterior. |
| QA06-H04 | MEDIO, pendiente regla PO | preguntas-v1 acepta version99, lista vacía, IDs duplicados, OpcionMultiple sin opciones, Abierta sin rúbrica, enunciado vacío y criterio negativo. | 7 caracterizaciones PASS confirman permisividad, NO aceptación. Acordar restricciones antes de modificar schema. No existe esquema de respuestas de alumnos aquí; es esquema de preguntas/opciones correctas. |
| QA06-H05 | MEDIO | Swagger Evaluaciones/Sala publica200 sin respuestas tipadas para acciones que devuelven201/204/400/404/409; IA health sin body; sin snapshot congelado para Identidad/Evaluaciones/Sala. Cursos dominio tiene longitudes no expresadas por DTO y DELETE400 no congelado. | ABIERTO: responsables de contratos actualizar anotaciones y snapshots; diff estructural guarda campos/tipos/required para revisión. Ausencia de snapshot no equivale a funcionalidad rota. |
| QA06-H06 | BAJO | Tres WeatherForecast de plantilla sin requisito identificado. | PENDIENTE POR ALCANCE: evaluar retirar al preparar despliegue; no crear tareas funcionales inexistentes. |
| QA06-H07 | MEDIO | Cohorte201 Location apunta a lista; Sala201 apunta a GET que no existe en main. | ABIERTO: acordar URI de recurso y alcance de GET; no implementar endpoint futuro desde QA. |
| QA06-H08 | ALTO, alcance por confirmar | Cohorte acepta docenteId no vacío sin verificar su institución en Identidad; matrículas tampoco consultan en Identidad la pertenencia del alumno. | PENDIENTE POR ALCANCE: verificar contrato interservicio y pertenencia antes de certificar integridad referencial global. QA-04 prueba acceso a registros existentes, no la validez de cualquier referencia externa. |

En esta rama QA-06 no se cambió código de producción ni los contratos congelados. No se corrigieron por conjetura reglas de negocio. Sí se agregaron validación automática y evidencia reproducible.

## JSON Schema y pruebas

Draft2020-12 compilado por AJV con formatos UUID. Dos preguntas soportadas: Abierta/OpcionMultiple. Version entero requerido; preguntas array requerido; cada pregunta exige id/enunciado/tipo/puntajeMaximo≥0. Opciones, si existen, exigen id/texto/esCorrecta booleano. Criterios de rúbrica, si existen, exigen descripción/puntaje numérico. No hay condicionales por tipo ni exigencia de suma de puntajes. Los 17 casos prueban ejemplos válidos, ausencias, tipos inválidos, UUID, enum y valores negativos. 12 casos xUnit adicionales ejecutan `ValidadorPreguntas` real con el recurso embebido y RequireFormatValidation. No se ensayó corrección de respuestas por un proveedor LLM.

| Comando (desde raíz) | Total | PASS | FAIL | Omitidas |
|---|---:|---:|---:|---:|
| `npm test --prefix tests/contracts` | 26 | 26 | 0 | 0 |
| `dotnet test services/evaluaciones/tests/PEI.Evaluaciones.UnitTests` | 13 | 13 | 0 | 0 |
| `dotnet test services/identidad/tests/PEI.Identidad.UnitTests --no-build` | 169 | 169 | 0 | 0 |
| `dotnet test services/ia/tests/PEI.IA.UnitTests --no-build` | 21 | 21 | 0 | 0 |

229 casos ejecutados en total; uno de Evaluaciones es un test vacío preexistente, por lo que no cuenta como cobertura nueva. Build solución:0 errores. Captura/validación:5/5 Swagger; coincidencia de rutas/métodos:14/14 congelados. Ninguna de estas métricas equivale al porcentaje de conformidad de negocio.

## Criterios de entrega y aprobación

6 criterios técnicos definidos: (1) inventario de contratos, (2) comparación de las 35 operaciones, (3) ejemplos schema válidos/ inválidos, (4) matriz trazable, (5) hallazgos priorizados y evidencia, (6) consulta de requisitos externos completos. Verificados5/6 =83,3%; el sexto está bloqueado por acceso. Aprobación humana es criterio adicional:0/1. No se declara100% ni cierre de QA-06. Newman/aislamiento y el reporte consolidado se entregan en el PR107 de QA-04.

**PENDIENTE DE VALIDACIÓN POR PRODUCT OWNER**

Nicolás Monasterio debe revisar: matriz de roles, reglas mínimas de preguntas, contrato de errores, frontera de confianza de servicios, alcance de integraciones y documentos externos. Evidencia de aprobación: ninguna al 24/10/2026. Firma/fecha/enlace de aprobación: pendientes. La incorporación de pruebas/documentación puede revisarse independientemente del cierre funcional; no autoriza producción ni merge automático.

## Actualización con evidencia QA-04 — 24/10/2026

El [PR107](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/107) verifica los diez casos de aislamiento: 39 solicitudes HTTP, 54 aserciones Newman y 317 casos xUnit, todos PASS y sin omisiones. La ejecución inicial conservada encontró dos diferencias que la revisión estática no detectó completamente:

- QA04-H01: la intención del filtro cerrado era lista vacía/detalle404, pero EF evaluaba Nullable.Value anticipadamente y devolvía409 sin tenant; con contexto nulo arrojaba NullReferenceException. Se corrige en QA-04, con seis regresiones de traducción y verificación HTTP de las tres entidades. La descripción anterior es la intención contractual, no una afirmación de que el main original pasara QA04-09.
- QA04-H02: POST cohorte bajo curso ajeno devolvía400 pese al404 explícito del contrato. QA-04 cambia únicamente esa excepción y conserva400 para curso propio inactivo.

La matriz anterior documenta la base853f3f6. Los casos marcados “previsto/pendiente en QA-04” ahora tienen evidencia PASS en ese PR; no se atribuye el comportamiento corregido al main original. Ninguno de estos dos arreglos está incluido en QA-06. Ambos PR permanecen sin merge.

[Informe consolidado de Candela Luna](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/blob/feature/QA-04-tests-aislamiento/docs/qa/INFORME_QA04_QA06_Candela_Luna.md). La recuperación web alternativa de los enlaces externos tampoco pudo acceder a su contenido. La validación del PO continúa pendiente.