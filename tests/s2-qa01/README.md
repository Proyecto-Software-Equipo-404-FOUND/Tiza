# S2-QA-01 — pruebas y límites de aceptación

Responsable: Candela Luna (`candela1805`). Revisión: Ariel (`aacarriego`). Refs #150.

Base revisada: `origin/main` en `8439307`, 11/11/2026. No hay AGENTS.md en el árbol ni en sus directorios padre. Se conservaron los archivos locales de la rama QA-07 usando un worktree separado. Se reutilizan la colección de Cursos, el entorno Postman de Identidad, el lockfile Newman de QA-05 y los workflows QA-05/QA-06.

## Resultados locales del 11/11/2026

| Comando | Resultado |
| --- | --- |
| `npm test --prefix tests/contracts` | 33/33, sin omitidas |
| `node tests/s2-qa01/run-api.mjs` | 12 requests y 20 aserciones Newman; 0 fallos; PostgreSQL confirma 2 entregas totales después de reenviar ambos lotes; contenedor eliminado |
| `dotnet test services/sala-en-vivo/tests/PEI.SalaEnVivo.UnitTests --filter FullyQualifiedName~EnviarRespuestaCommandHandlerTests` | 11/11, sin omitidas |
| `dotnet test services/evaluaciones/tests/PEI.Evaluaciones.UnitTests` | 32/32, sin omitidas |
| `npm run build --prefix frontend` | TypeScript y Vite exitosos |
| `node tests/s2-qa01/signalr-idempotencia.mjs` | Bloqueado: faltan token/PIN y sala activa sembrada; no se inició la prueba |

Dos primeros intentos del runner API agotaron el timeout de `docker info` (30 s); se reemplazó ese diagnóstico por `docker version --format '{{.Server.Version}}'`. El tercer intento pasó completo con la API y base reales. Los warnings de xUnit por `.Result` en tests preexistentes y las anotaciones PURE de SignalR/Rollup no impidieron esas verificaciones.

## Ejecutar lo independiente

Requisitos: Node 20, .NET 8, Docker con contenedores Linux y `dotnet-ef` 8.0.11 (o una revisión compatible de .NET 8). Todos los comandos parten de la raíz del repositorio.

```sh
npm ci --prefix tests/contracts
npm test --prefix tests/contracts
npm ci --prefix tests/identity-negative
dotnet tool install --global dotnet-ef --version 8.0.11
node tests/s2-qa01/run-api.mjs
dotnet test services/sala-en-vivo/tests/PEI.SalaEnVivo.UnitTests --filter FullyQualifiedName~EnviarRespuestaCommandHandlerTests
npm ci --prefix frontend
node frontend/cypress/run-ui.mjs
```

En esta máquina Windows, npm necesitó `$env:NODE_USE_SYSTEM_CA='1'` para confiar en los certificados del sistema. El primer `npm ci --prefix tests/contracts` falló con `UNABLE_TO_VERIFY_LEAF_SIGNATURE`; no se deshabilitó TLS. El primer `docker info --format '{{.ServerVersion}}'` falló porque no estaba iniciado Docker Desktop; se inició y se volvió a intentar.

El runner API crea PostgreSQL 16 descartable en 5543, aplica migraciones reales, inicia Evaluaciones en 18543 y elimina exclusivamente su contenedor/volumen al terminar. Genera las claves en memoria, no acepta una base externa ni utiliza las credenciales del repositorio. Los puertos deben estar libres. Cada corrida genera salas/lotes nuevos y reutiliza los mismos identificadores al reenviar. Se conserva sólo evidencia sanitizada en `tests/s2-qa01/results/newman.json`.

Newman ejecuta 12 requests: crear/publicar evaluaciones A/B, recibir cada lote, reenviarlo y comparar los IDs/resultados, rechazar la institución incorrecta y rechazar la ausencia de clave interna. Después se consulta PostgreSQL para exigir exactamente dos entregas persistidas. Esto prueba el receptor de lotes, **no** el cierre de Sala. Evaluaciones aún usa `CursosClientProvisorio`; estas pruebas no prueban validación remota de cohortes.

Las carpetas nuevas están en `tests/postman/PEI-Cursos.postman_collection.json`. Para regenerarlas: `node tests/s2-qa01/generate.mjs`. El generador conserva las carpetas anteriores. `run-newman.mjs` carga el entorno existente `identidad.local.postman_environment.json` y agrega variables efímeras en memoria; nunca exporta claves ni JWT. Para correrlo manualmente contra una API local descartable ya preparada, configurar `QA_EVALUACIONES_URL` y `QA_INTERNAL_API_KEY` y ejecutar `node tests/s2-qa01/run-newman.mjs`.

## Cypress: UI y E2E real son resultados distintos

`run-ui.mjs` inicia Vite en 3000 con el MSW **existente** de la aplicación y una contraseña aleatoria que sólo sirve para esa demo. Se prueba la navegación y el formulario real: login docente → crear curso → crear cohorte con el docente canónico → matricular Alumno 1 A → comprobar una fila Activa y que desaparece del selector → volver a entrar a la matrícula. Los datos fijos se restablecen al cargar una instancia nueva del frontend. El test no intercepta ni fabrica respuestas.

Este modo prueba la UI; **no certifica login, permisos ni persistencia del backend**. CI lo nombra explícitamente. La prueba verifica si hay MSW activo para impedir confundir ambos modos.

Para el modo real, iniciar los servicios y el gateway con una instancia descartable de las semillas ID-08/CU-06, Vite con `VITE_USE_MOCKS=false`, y proporcionar `QA_DOCENTE_PASSWORD` desde el entorno (nunca en Git). Usar `QA_CYPRESS_MODE=live`, opcionalmente `QA_DOCENTE_EMAIL`, y:

```sh
cd frontend
npx cypress run --browser electron
```

No se marca exitoso hasta que termine ese comando contra los servicios. En la base revisada está bloqueado: `listarAlumnos` pide `/api/v1/identidad/alumnos`, mientras Identidad publica `/api/usuarios?rol=Alumno`; la tabla espera `alumnoNombre`, ausente en `MatriculacionDto`. El PR #206 (`feature/FE2-11-matriculas-contrato-real`) trata esa compatibilidad. Integrarlo y verificar el nombre canónico del alumno antes de repetir. No se sustituyó la API con un servidor falso para presentar el caso como E2E real.

## Sala: idempotencia y cierre

El handler real de `EnviarRespuesta` tiene un test nuevo que invoca dos veces el mismo comando y exige `Aceptada`, luego `Duplicada`, timestamp original y una sola entidad guardada. Usa los dobles de repositorio existentes: no acredita PostgreSQL ni transporte SignalR.

La prueba de transporte usa el cliente oficial SignalR y los nombres del contrato (`UnirseASala`, `ObtenerEstadoSala`, `EnviarRespuesta`), no un REST ficticio:

```sh
node tests/s2-qa01/signalr-idempotencia.mjs
```

Requiere `QA_ALUMNO_TOKEN`, `QA_SALA_PIN`, una sala activa local con una pregunta vigente que el alumno aún no respondió y matrícula válida; opcionalmente `QA_HUB_URL` y `QA_RESPUESTA` (texto/ID de opción válido). Comprueba Aceptada→Duplicada, misma clave/timestamp, una sola pregunta respondida y una única progresión de secuencia. No finaliza la sala. La ejecución local sin esos datos falla explícitamente: `Faltan QA_ALUMNO_TOKEN y QA_SALA_PIN de sala activa de prueba`. No hay éxito ni skip silencioso.

La issue #140 y el PR #202 (`metodos-salahub`) estaban abiertos al revisar. `main` tiene dominio/DTOs/repositorios, pero no `FinalizarSala` en `SalaHub`. Falta integrar el cierre, su envío autenticado a Evaluaciones y sus pruebas. Después: crear/publicar evaluación, abrir sala y matricular al alumno, responder, finalizar como docente, observar `salaFinalizada`, verificar el lote persistido, repetir cierre/reenvío y comprobar que no duplica. No se implementó la funcionalidad de Tomás ni se simuló su resultado.

## Visibilidad de entregas pendiente de integración

El PR #205 (`feature/S2-EV-03-consultar-resultados`) agrega los GET `/api/v1/evaluaciones/{id}/entregas` y `.../{entregaId}`; aún no existen en la base revisada. La carpeta `S2-QA-01 Visibilidad de entregas (requiere S2-EV-03)` incluye controles positivos A/B y acceso cruzado por listado e ID. Un 404 aislado **no** prueba aislamiento: cada institución primero debe leer su propia entrega con 200 y un alumno esperado.

Después de integrar #205, configurar dos JWT docentes reales (`QA_TOKEN_A`, `QA_TOKEN_B`) de las instituciones canónicas, `QA_TEST_VISIBILITY=true`, la API local y su clave interna. `run-newman.mjs` ejecutará las dos carpetas (18 requests) en la misma sesión. Hasta entonces reporta el caso como bloqueado y el PR permanece en borrador. No se confunde el rechazo de un lote ajeno con la visibilidad de entregas.

## CI y cierre

`qa05-newman.yml` conserva QA-05 y agrega Newman con API/PostgreSQL reales, idempotencia del handler y Cypress UI. `qa06-contratos.yml` ejecuta automáticamente los nuevos ejemplos. El PR usa `Refs #150`, no `Closes`, hasta completar Cypress real, SignalR con sala sembrada, visibilidad tras #205, cierre tras #202, CI y aprobación de Ariel. k6 queda fuera de alcance.
