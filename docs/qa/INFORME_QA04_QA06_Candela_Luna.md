# Informe técnico QA-04 y QA-06 — Candela Luna

Fecha de presentación: 24/10/2026. Repositorio: Proyecto-Software-Equipo-404-FOUND/Tiza. Base común: `853f3f6`. Las marcas horarias originales de las evidencias provienen del reloj de ejecución y se conservan sin alterarlas.

## A. Resumen ejecutivo

Se leyó el backlog `D:/Downloads/Backlog Completo y Plan de Sprints - PEI (Tiza) - 404 Found.xlsx`, sin modificarlo. Se actualizó main mediante fetch y pull fast-forward, sin cambios locales previos que sobrescribir. Se revisaron arquitectura, backlog y ramas/PR relacionados (incluidos contratos CU-07 y semillas CU-06), sin encontrar entregas previas QA-04/QA-06. Se crearon dos ramas independientes desde la misma base.

QA-06 incorpora validación de OpenAPI y JSON Schema, pruebas del validador real de Evaluaciones, cinco Swagger capturados y matriz de35 operaciones. Quedan decisiones funcionales, acceso a requisitos externos y aprobación del PO pendientes. QA-04 reutiliza las semillas CU-06/ID-08 y prueba APIs reales con PostgreSQL descartable; los JWT positivos son emitidos por Identidad. La primera ejecución descubrió dos defectos reproducibles, corregidos sin alterar las expectativas: filtro sin tenant que arrojaba excepciones y creación de cohorte en curso ajeno que respondía400 en vez del404 documentado.

**PENDIENTE DE VALIDACIÓN POR PRODUCT OWNER — Nicolás Monasterio.** No existe evidencia de su aprobación. Los resultados de Cursos/Identidad no certifican la seguridad de Evaluaciones, Sala o IA.

## B. Estado de tareas

| Tarea | Estado anterior | Estado final | Evidencia |
|---|---|---|---|
| QA-04 | Backlog: Pendiente; sin entrega verificada | COMPLETADA técnicamente; PR pendiente de revisión | colección, runner, workflow y evidencias antes/después |
| QA-06 | Backlog: En Curso; sin revisión verificada | PARCIAL: revisión técnica, pendiente requisitos externos y PO | PR106, revisión contractual y evidencias de5 servicios |

La revisión contractual completa está en [QA06_REVISION_CONTRATOS.md, rama QA-06](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/blob/feature/QA-06-revision-contratos/docs/qa/QA06_REVISION_CONTRATOS.md). El reporte consolidado se entrega en QA-04 para no duplicarlo en ambas ramas.

## C. Cambios realizados

Las rutas completas y motivos por archivo se enumeran en el anexo al final. Cambios funcionales acotados:

- `CursosDbContext`: extrae el tenant a una propiedad nullable segura; las tres consultas siguen cerradas sin institución válida. Elimina la evaluación anticipada de `.Value` y la desreferencia de contexto nulo, sin agregar migraciones ni acceso global.
- `CrearCohorteCommandHandler`: un curso ausente/ajeno lanza KeyNotFoundException, que el middleware existente traduce a404 ProblemDetails. Un curso propio inactivo mantiene400. El contrato congelado distingue explícitamente esos casos.
- Se ajustó el test unitario existente al contrato404 y se agregaron seis regresiones de traducción Npgsql sin conexión. Newman comprueba el comportamiento real sobre PostgreSQL.

No se alteraron contratos congelados, reglas de preguntas, roles, seeds existentes, datos de desarrollo ni main. QA-06 contiene sólo pruebas, automatización y documentación. Las correcciones de producción pertenecen exclusivamente a QA-04 y están justificadas por evidencia fallida previa.

## D. Pruebas y matriz de aceptación

### Entorno y reproducibilidad

.NET8; Node20; Newman6.2.1; PostgreSQL16-alpine. Runner: `node tests/tenant-isolation/run.mjs`; instrucciones completas en [README](../../tests/tenant-isolation/README.md). Cada ejecución crea contenedor y etiqueta UUID, puerto127.0.0.1:5541, bases `pei_identidad_tests` y `pei_cursos_qa04`, esquema `qa04_seed`. Usa contraseñas/clave aleatorias en memoria y verifica etiqueta antes de limpiar sólo el contenedor propio. Se preservaron los contenedores y volúmenes de desarrollo. Los puertos API18541/18542 son locales.

Los resúmenes no guardan contraseñas, JWT ni bodies de login. Contienen método, ruta, rol, tenant, esperado/obtenido y aserciones. Las versiones de las fuentes críticas se identifican mediante hashes SHA256; `baseCommit` puede ser el padre mientras las modificaciones aún no estaban confirmadas. Los hashes permiten relacionar la ejecución con el contenido finalmente publicado.

### Registro completo de ejecuciones

| Etapa / comando | Total | PASS | FAIL | Omitidas | Interpretación |
|---|---:|---:|---:|---:|---|
| QA-06 `dotnet build PEI.sln --nologo -v quiet -m:1` | compilación | 0 errores | 0 | — | solución compilada |
| QA-06 `npm test --prefix tests/contracts` | 26 | 26 | 0 | 0 |2 OpenAPI,17 schema,7 caracterizaciones de brechas |
| QA-06 `dotnet test services/evaluaciones/tests/PEI.Evaluaciones.UnitTests` | 13 | 13 | 0 | 0 |12 nuevas;1 test vacío preexistente |
| QA-06 `dotnet test services/identidad/tests/PEI.Identidad.UnitTests --no-build` | 169 | 169 | 0 | 0 |incluye contrato Swagger y HTTP |
| QA-06 `dotnet test services/ia/tests/PEI.IA.UnitTests --no-build` | 21 | 21 | 0 | 0 |sin proveedor LLM real |
| QA-06 `npm run capture --prefix tests/contracts` + `npm run compare --prefix tests/contracts` |5 Swagger /35 operaciones |5 válidos |0 de formato |—|14/14 rutas/métodos congelados presentes, no conformidad semántica total |
| QA-04 primera ejecución `node tests/tenant-isolation/run.mjs` |33 solicitudes /48 aserciones |28 solicitudes /41 aserciones |5 solicitudes /7 aserciones |0|QA04-09 y POST cohorte ajena; evidencia conservada |
| Primera ejecución: `dotnet test` Cursos.UnitTests / Identidad.UnitTests / Identidad.IntegrationTests |77+169+65=311|311|0|0|no detectaban los nuevos defectos HTTP |
| `dotnet test services/cursos/tests/PEI.Cursos.UnitTests --filter FullyQualifiedName~TenantQueryTranslationTests` antes de corregir filtro |6|0|6|0|NullReferenceException/InvalidOperationException reproducidas |
| Mismo comando después de corregir filtro |6|6|0|0|regresión dirigida |
| Intento intermedio del runner |0 solicitudes|0|1 error de infraestructura|—|PostgreSQL temporal respondió readiness antes de reiniciar; se corrigió esperando TCP y se repitió desde cero |
| Final `node tests/tenant-isolation/run.mjs`: Newman |39 solicitudes /54 aserciones|39 /54|0|0|APIs reales; ambos defectos corregidos |
| Final runner: `dotnet build PEI.sln --nologo -v quiet -m:1` |compilación|0 errores|0|—|todas las soluciones compiladas |
| Final runner: `dotnet test services/cursos/tests/PEI.Cursos.UnitTests --no-build` |83|83|0|0|incluye seis nuevas regresiones y caso404 actualizado |
| Final runner: `dotnet test services/identidad/tests/PEI.Identidad.UnitTests --no-build` |169|169|0|0|regresión existente |
| Final runner: `dotnet test services/identidad/tests/PEI.Identidad.IntegrationTests --no-build` |65|65|0|0|PostgreSQL real, sin saltos por falta de conexión |

Los TRX locales se guardan en `TestResults/qa06*`, `TestResults/qa04-reproduccion` y `tests/tenant-isolation/results`. CI publica únicamente evidencia sanitizada QA-04. Las colecciones previas de Identidad/Cursos se parsearon y cargaron mediante Postman SDK; no se ejecutaron todos sus flujos. La nueva colección sí se ejecuta completa con Newman. Los ejemplos JSON Schema prueban UUID, enum, tipos, propiedades requeridas, opciones/esCorrecta y rúbricas. Las siete permisividades registradas no son aprobaciones funcionales.

### Matriz QA-04

| Criterio | Contrato / implementación | Prueba y expectativa fijada antes de ejecución | Resultado final |
|---|---|---|---|
| QA04-01 A consulta sus cursos | cursos-v1 GET cursos / filtro EF |200, exactamente dos cursos de A, ningún B|PASS|
| QA04-02 A consulta curso B | cursos-v1 GET{id} / CursosController |404, sin atributos de B|PASS|
| QA04-03 A lista cohortes B | cursos-v1 GET cursos/{id}/cohortes |404, padre no visible|PASS|
| QA04-04 A modifica curso B | cursos-v1 PUT curso / handler filtrado |404 y lectura posterior B sin cambios|PASS|
| QA04-05 A modifica cohorte B | cursos-v1 PUT cohorte / handler filtrado |404 y lectura posterior B sin cambios|PASS|
| QA04-06 B consulta datos A | filtros simétricos cursos/cohortes |404 en detalle y lista ajenos|PASS|
| QA04-07 recurso sin JWT | Authorize/JwtBearer |401|PASS|
| QA04-08 JWT inválido | validación de firma |401 con firma incorrecta|PASS|
| QA04-09 contexto ausente/inválido/vacío | filtro cerrado + validación comando |lista200 vacía, detalles curso/cohorte/matrícula404, escritura400|PASS|
| QA04-10 recursos propios según rol | Docente A real + endpoint de rol Identidad |curso/cohorte200; Docente200 y Administrador403 en Identidad|PASS|

Extras: DELETE curso/cohorte ajenos404, POST cohorte ajena404, validación cohorte/matrícula ajenas404, PUT inverso B→A404, header institucional B no suplanta JWT A. Los tokens sin tenant se firman sólo con la clave efímera del entorno negativo; no se modifica la autenticación de producción para permitirlos.

## E. Hallazgos y defectos

| ID | Severidad | Ubicación y descripción | Corrección / estado |
|---|---|---|---|
| QA04-H01 | MEDIO | CursosDbContext, tres filtros: EF evalúa Nullable.Value antes de los &&; HTTP lista/detalle sin tenant devolvía409; sin objeto contexto arroja NullReferenceException. No se observó fuga de registros. |Propiedad nullable segura y filtro cerrado; seis pruebas dirigidas pasan; CORREGIDO: validación HTTP final PASS |
| QA04-H02 | MEDIO | CrearCohorteCommandHandler devuelve400 cuando el contrato especifica404 para curso ausente en el tenant. |KeyNotFoundException y ajuste del unitario; conserva400 para inactivo; CORREGIDO: validación HTTP final PASS |
| QA04-H03 | BAJO | Readiness del runner detectaba servidor temporal de initdb por socket. |Espera TCP, limpieza y nueva ejecución; es defecto del harness, no del producto |
| QA06-H01 | MEDIO |Errores Cursos/IA y anotaciones Swagger no completamente coherentes con ProblemDetails/401 congelados. |ABIERTO; unificar contrato y middleware/controladores en tarea correspondiente |
| QA06-H02 | ALTO |Cursos sólo exige Authorize, sin política por rol para mutaciones. |ABIERTO; PO y responsable Cursos deben acordar matriz y probar roles negativos. QA04-10 no certifica todos los permisos |
| QA06-H03 | CRÍTICO si se expone como API multi-tenant |Evaluaciones confía en header y Sala en body; IA declara uso interno sin JWT. |PENDIENTE POR ALCANCE/INTEGRACIÓN; verificar frontera de confianza antes de producción |
| QA06-H04 | MEDIO, regla PO pendiente |Schema permite siete casos potencialmente inválidos para negocio. |ABIERTO; no endurecer reglas sin aprobación |
| QA06-H05 | MEDIO |Swagger incompleto en Evaluaciones/Sala; faltan snapshots congelados; restricciones de dominio omitidas. |ABIERTO; completar anotaciones y versionado |
| QA06-H06 | BAJO |WeatherForecast de plantilla en tres servicios. |Pendiente por alcance, no funcionalidad de negocio fallida |
| QA06-H07 | MEDIO |Location cohorte apunta a lista; Location Sala apunta a GET inexistente. |ABIERTO; acordar URI/alcance |
| QA06-H08 | ALTO, alcance pendiente |No se verifica en Identidad la pertenencia del docente/alumno al crear referencias en Cursos. |Integración pendiente; el aislamiento de registros existentes no prueba integridad de referencias externas |

No se implementaron funcionalidades de sprints futuros ni se declararon fallidas sólo por no estar disponibles. No se resolvieron los hallazgos QA-06 cambiando expectativas de QA-04.

## F. Pendientes, responsables y riesgos

- Nicolás Monasterio: revisión de criterios y aprobación explícita. Estado: **PENDIENTE DE VALIDACIÓN POR PRODUCT OWNER**. Sin firma ni enlace de aprobación.
- Candela/PO: facilitar acceso a los documentos Drive enlazados en el backlog. El conector no estaba disponible y la recuperación web alternativa tampoco pudo acceder; no se inventaron sus criterios. IDs: QA04 `1YzsQDDvWe-zrDJjizb67Jhm6CoA4HyZDcN-PHkx0FdY`, QA06 `1liYLFPP5AlGAvTe4OLp1M8Xm5fj4t4pL`.
- Responsables de Cursos/seguridad: matriz de roles, pertenencia de referencias externas y uniformidad de errores.
- Responsables Evaluaciones/Sala/IA: frontera de autenticación, contratos tipados y alcance de integración.
- Equipo revisor: revisar los dos cambios funcionales pequeños de QA-04 y aprobar/mergear cuando corresponda. No se ejecutó merge.

La prueba es directa a los servicios locales, no una prueba del gateway, frontend, despliegue productivo, concurrencia de Cursos o carga. No se llamó a un proveedor real de IA ni se enviaron correos. No se exportaron tokens ni credenciales. Estas limitaciones no se contabilizan como pruebas aprobadas.

## G. GitHub

| Tarea | Rama | Commit de implementación | PR a main | Integración |
|---|---|---|---|---|
| QA-06 |feature/QA-06-revision-contratos|9af913c; a611b86 (documentación)|[PR106](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/106)|borrador, sin merge; CI contracts/backend/frontend PASS en revisión inicial|
| QA-04 |feature/QA-04-tests-aislamiento|a082827; 3a9d7cc (enlaces)|[PR107](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/107)|sin merge|

La simulación `git merge-tree --write-tree feature/QA-04-tests-aislamiento feature/QA-06-revision-contratos` terminó sin conflictos; no modifica ramas ni integra los PR. No se hizo force-push ni se modificó main directamente. Las actualizaciones finales de este mismo informe son commits documentales posteriores, visibles en el historial de cada PR. Tras el último cambio documental, CI vuelve a ejecutarse; los checks del HEAD actual se consultan en los PR y no se infiere PASS de una ejecución anterior. Los PR contienen sólo sus cambios de tarea; el informe consolidado se aloja en QA-04. La condición de mergeable de GitHub sólo expresa ausencia de conflictos, no aprobación funcional.

## H. Conclusión

1. **QA-04:** completada técnicamente para los diez escenarios solicitados; entrega publicada para revisión del equipo, sin merge.
2. **QA-06:** no está completamente terminada. Se realizó la revisión técnica, pero faltan requisitos externos completos y aprobación del PO.
3. **Porcentajes:** QA-04 se calcula sobre los10 escenarios identificados QA04-01…10; 10/10 PASS = 100% en el alcance declarado, sin extrapolar a roles no ensayados ni otros servicios. QA-06:5/6 criterios técnicos verificables=83,3% (inventario, comparación35 operaciones, ejemplos schema, matriz, hallazgos/evidencia; falta acceso a requisitos completos). Aprobación PO es un criterio adicional separado:0/1. La coincidencia14/14 de rutas congeladas no equivale a100% de conformidad funcional.
4. **Pruebas:** se conservan fallos iniciales y evidencia posterior; resultado final PASS: 54/54 aserciones Newman y 317/317 casos xUnit en QA-04, sin omisiones; QA-06 229/229 casos, con las salvedades documentadas. Identidad169 se repite entre etapas, no sumar como pruebas distintas.
5. **Defectos pendientes:** sí, los ocho hallazgos QA-06 con estados detallados; no se ocultan tras los PASS técnicos.
6. **Integración:** técnicamente viable y sin conflictos en la simulación; las pruebas locales pasan. No se considera aprobada todavía la integración de ambos PR: falta revisión del equipo y validación del PO para cerrar QA-06. No se certifica producción ni seguridad de toda la plataforma. No se realizó merge.
7. **Acción manual de Candela:** coordinar validación con Nicolás, recuperar los requisitos externos, revisar los PR y solicitar integración al equipo. No necesita editar contraseñas ni bases reales para reproducir la suite.

## Anexo — archivos y rutas completas

| Tarea | Ruta completa | Motivo / descripción técnica |
|---|---|---|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/.github/workflows/qa06-contratos.yml`|Pipeline reproducible en Node20/.NET8 y publicación de evidencias de la tarea.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/QA06_REVISION_CONTRATOS.md`|Revisión por endpoint, matriz de trazabilidad, hallazgos y aprobación PO pendiente.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/cursos.json`|Snapshot del Swagger de este servicio capturado sin conexión a bases reales.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/endpoints.json`|Inventario de35 operaciones y respuestas documentadas frente a Swagger actual.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/endpoints.md`|Inventario de35 operaciones y respuestas documentadas frente a Swagger actual.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/evaluaciones.json`|Snapshot del Swagger de este servicio capturado sin conexión a bases reales.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/ia.json`|Snapshot del Swagger de este servicio capturado sin conexión a bases reales.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/identidad.json`|Snapshot del Swagger de este servicio capturado sin conexión a bases reales.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/sala-en-vivo.json`|Snapshot del Swagger de este servicio capturado sin conexión a bases reales.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa06/semantic-review.json`|Diferencias estructurales desreferenciadas: parámetros, schemas, tipos, required, respuestas y seguridad.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/services/evaluaciones/tests/PEI.Evaluaciones.UnitTests/ContratoPreguntasTests.cs`|Doce casos del validador real y schema embebido de Evaluaciones.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/.gitignore`|Excluye dependencias y resultados locales de ejecución.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/README.md`|Prerequisitos, comandos, límites del alcance y tratamiento de secretos/evidencia.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/capture.mjs`|Captura los cinco Swagger con conexiones inaccesibles y credenciales efímeras.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/compare.mjs`|Valida y compara rutas, métodos y estructuras con contratos congelados.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/contracts.test.mjs`|26 validaciones OpenAPI/schema, incluyendo7 permisividades explícitas no aprobadas.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/package-lock.json`|Fija árbol de dependencias npm e integridades para npm ci.|
|QA-06|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/contracts/package.json`|Define dependencias y comandos de la suite.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/.github/workflows/qa04-aislamiento.yml`|Pipeline reproducible en Node20/.NET8 y publicación de evidencias de la tarea.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/INFORME_QA04_QA06_Candela_Luna.md`|Informe consolidado, estados, denominadores, hallazgos, resultados y GitHub.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa04/antes-correccion.json`|Evidencia sanitizada inicial:33 solicitudes y7 aserciones fallidas; conserva trazabilidad del defecto.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa04/antes-correccion.md`|Evidencia sanitizada inicial:33 solicitudes y7 aserciones fallidas; conserva trazabilidad del defecto.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa04/intento-infraestructura.json`|Registra intento abortado por readiness PostgreSQL; ninguna solicitud funcional ejecutada.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa04/reproduccion-filtro.json`|Seis fallos de traducción antes y seis PASS después; sin secretos ni conexión real.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa04/resultado-final.json`|Evidencia sanitizada posterior:39 solicitudes,54 aserciones,317 xUnit y limpieza; JSON incluye hashes.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/docs/qa/evidencias/qa04/resultado-final.md`|Evidencia sanitizada posterior:39 solicitudes,54 aserciones,317 xUnit y limpieza; JSON incluye hashes.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/services/cursos/src/PEI.Cursos.Application/UseCases/Cohortes/Commands/CrearCohorteCommandHandler.cs`|Corrige excepción para responder404 según contrato cuando el padre no es visible.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/services/cursos/src/PEI.Cursos.Infrastructure/Persistence/CursosDbContext.cs`|Evita evaluación de Nullable.Value/contexto nulo; conserva aislamiento cerrado de tres entidades.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/services/cursos/tests/PEI.Cursos.UnitTests/Application/Cohortes/CrearCohorteCommandHandlerTests.cs`|Actualiza regresión existente: curso ausente debe mapear a404; conserva prueba400 para inactivo.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/services/cursos/tests/PEI.Cursos.UnitTests/Infrastructure/TenantQueryTranslationTests.cs`|Seis regresiones sin conexión: tres entidades con tenant/contexto ausente.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/services/cursos/tests/PEI.Cursos.UnitTests/PEI.Cursos.UnitTests.csproj`|Referencia Infrastructure para probar traducción del proveedor Npgsql real.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/postman/PEI-QA04-Aislamiento.postman_collection.json`|Colección importable de39 solicitudes para aislamiento; sólo variables, sin secretos.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/tenant-isolation/.gitignore`|Excluye dependencias y resultados locales de ejecución.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/tenant-isolation/README.md`|Prerequisitos, comandos, límites del alcance y tratamiento de secretos/evidencia.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/tenant-isolation/generate-collection.mjs`|Fuente reproducible de la colección, expectativas y aserciones; CI evita divergencia.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/tenant-isolation/package-lock.json`|Fija árbol de dependencias npm e integridades para npm ci.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/tenant-isolation/package.json`|Define dependencias y comandos de la suite.|
|QA-04|`C:/Users/cande/OneDrive/Desktop/Facu/proyecto software/Tiza/tests/tenant-isolation/run.mjs`|Orquesta Docker descartable, migraciones/semillas existentes, APIs, Newman, regresión, evidencia y limpieza.|
