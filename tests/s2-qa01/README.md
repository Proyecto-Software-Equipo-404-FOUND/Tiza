# S2-QA-01 — pruebas reproducibles

Responsable: Candela Luna (`candela1805`). Revisor: Ariel (`aacarriego`). PR #207.

## Estado actual

En la revisión inicial de `8439307`, #202, #205 y #206 estaban abiertos. Al retomar se verificó que los tres están integrados y #140 cerrada. La rama incorpora `origin/main` en `c87ff8c`. No se implementó funcionalidad de otro integrante; se habilitaron los casos reales sobre sus integraciones.

## Reproducción desde la raíz

Requisitos: Node 20, .NET 8, Docker Linux y dotnet-ef 8.0.11 (o revisión compatible).

```sh
npm ci --prefix tests/contracts
npm test --prefix tests/contracts
npm ci --prefix tests/identity-negative
npm ci --prefix frontend
# Si todavía no está instalado:
dotnet tool install --global dotnet-ef --version 8.0.11
node tests/s2-qa01/run-api.mjs
node tests/s2-qa01/run-live.mjs
# Regresión UI adicional y separada:
node frontend/cypress/run-ui.mjs
```

`run-api.mjs` usa 5543/18543. `run-live.mjs` usa 5541, 18641–18644, 8080 y 3000. Exigen puertos libres, crean contenedores/bases descartables y claves efímeras, y verifican la etiqueta del contenedor antes de limpiarlo. No usan bases ni claves de desarrollo. No ejecutar QA04 y el runner completo simultáneamente (5541), ni dos runners Cypress (3000).

El runner completo reutiliza la preparación de QA04: migraciones reales, semillas ID-08/CU-06 y JWT emitidos por Identidad. Levanta Identidad, Cursos, Evaluaciones, Sala y el gateway real. Sala usa su fallback de presencia en memoria: no se certifica Redis ni su recuperación.

## Cobertura y límites

- Contratos: ejemplos publicados de lote-entregas-v1 y resultado-lote-entregas-v1 contra draft 2020-12; negativos por campo obligatorio y UUID inválido.
- Newman: 18 requests / 28 aserciones. Crear/publicar A/B, recibir/repetir lotes, comparar IDs, rechazar institución ajena y falta de clave interna, leer entregas propias y rechazar listados/detalles ajenos. Los controles positivos impiden aceptar un 404 de una ruta inexistente como aislamiento. El runner API exige dos entregas en PostgreSQL después del reenvío.
- Cypress real: login docente, curso, cohorte con docente canónico, matrícula de Alumno uno Prueba A, una fila Activa, exclusión del selector y navegación de vuelta. El runner consulta después la matrícula por API para confirmar persistencia. No se interceptan APIs ni se usa MSW. Sólo se bloquean Google Fonts para utilizar fuentes locales.
- SignalR real: crear/publicar evaluación, crear sala, conectar docente/alumno matriculado, iniciar y reenviar el mismo payload/key. Exige Aceptada → Duplicada, timestamp original y una respuesta en PostgreSQL.
- Cierre real: alumno no puede finalizar; docente finaliza y se recibe salaFinalizada; Evaluaciones devuelve la entrega; repetir cierre conserva IDs y una fila SQL; se rechazan respuestas nuevas después del cierre.
- El test unitario del handler invoca dos veces el comando, separado de transporte/persistencia. Cypress UI con MSW es una regresión adicional y no acredita E2E real.

La evaluación es multiple choice, sin dependencia de IA. Evaluaciones conserva CursosClientProvisorio: no se acredita su validación remota de cohortes. k6 fuera de alcance.

## Postman y modos manuales

Se amplía `tests/postman/PEI-Cursos.postman_collection.json` conservando sus carpetas; se reutiliza `identidad.local.postman_environment.json` y el lockfile Newman de QA-05. Regenerar: `node tests/s2-qa01/generate.mjs`.

`node tests/s2-qa01/run-newman.mjs` requiere API local descartable (`QA_EVALUACIONES_URL`), `QA_INTERNAL_API_KEY` y JWT docentes `QA_TOKEN_A`/`QA_TOKEN_B`. La visibilidad corre por defecto. `QA_TEST_VISIBILITY=false` habilita una ejecución parcial, indicada en el reporte, que no satisface el criterio completo.

Cypress manual: Vite con `VITE_USE_MOCKS=false`, gateway en 8080, `QA_DOCENTE_PASSWORD` y opcionalmente `QA_DOCENTE_EMAIL`; ejecutar `npm run test:e2e --prefix frontend`. Deben existir las semillas canónicas y no debe existir el curso/cohorte S2-QA-01. El runner completo garantiza una base nueva. El modo live exige ausencia de MSW.

SignalR aislado: `node tests/s2-qa01/signalr-idempotencia.mjs`; requiere `QA_ALUMNO_TOKEN`, `QA_SALA_PIN`, sala EnCurso con pregunta vigente aún no respondida y opcionalmente `QA_HUB_URL`/`QA_RESPUESTA`. No finaliza la sala: usar el runner completo para cierre.

## Evidencia confirmada

Los reportes sanitizados `tests/s2-qa01/results/newman.json` y `live.json` están ignorados por Git y se publican como artifacts en Actions. No se exporta environment ni reportes crudos con claves/JWT. QA-05 incluye Newman, Cypress UI y el recorrido real; QA-06 ejecuta contratos.

- Contratos: 33/33; handler de respuestas: 11/11, sin omitidas.
- Base inicial: Evaluaciones 32/32 y build TypeScript/Vite exitoso.
- Tras integrar #205: Newman 18 requests / 28 aserciones, 0 fallos, 2 entregas SQL.
- Cypress UI local: 1/1, sin omitidas, al excluir las tipografías externas.
- Actions sobre `7cb23c1`: cinco workflows verdes, incluido Cypress UI.
- El runner completo todavía requiere registrar su ejecución final: exige completed=true, todas las aserciones y limpieza exitosa. No se presenta la UI como E2E real.

## Intentos y problemas locales

`npm ci --prefix tests/contracts` falló primero con UNABLE_TO_VERIFY_LEAF_SIGNATURE; se resolvió con `$env:NODE_USE_SYSTEM_CA='1'`, sin deshabilitar TLS. Docker Desktop estaba detenido; tras iniciarlo, docker info agotó 30 s en dos intentos; se usa docker version y el runner API pasó después.

Cypress tuvo una extracción incompleta; se completó y reparó con `npm ci --prefix frontend --ignore-scripts --offline --no-audit --no-fund`, y verificó su binario. `node frontend/cypress/run-ui.mjs` agotó 60 s esperando load con Google Fonts; al bloquear sólo las tipografías pasó.

El primer `node tests/s2-qa01/run-live.mjs` fue rechazado por ID-08: Testing requiere pei_identidad_tests local:5541. Se ajustó el fixture al puerto autorizado, sin cambiar el seeder. El intento fallido se limpió. SignalR aislado sin token/PIN falla explícitamente; el runner completo prepara esos datos.

No hay AGENTS.md en el árbol ni en sus padres. Se preservaron los archivos locales de QA-07 mediante un worktree separado. Sin merge del PR.
