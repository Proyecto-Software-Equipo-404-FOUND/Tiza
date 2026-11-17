# S2-QA-01 — pruebas reproducibles

Responsable: Candela Luna (`candela1805`). Revisor: Ariel (`aacarriego`). [PR #207](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/207).

## Estado y alcance

Revisión del 17/11/2026: QA-02 (#162, PR #108), QA-06 (#163, PR #106) y QA-07 (#164, PR #109) están cerradas e integradas en `main`; se verificaron tanto la API de GitHub como la ascendencia de sus merges. No se reimplementaron. #150 está cerrada, pero el PR #207 todavía necesita aprobación e integración. Las tareas abiertas #237 y #250 pertenecen al Sprint 3 y no forman parte de este trabajo.

El commit `87ed400` pasó los diez checks de Actions, incluido el recorrido real completo. Las evidencias sanitizadas están en [evidence/2026-11-17](evidence/2026-11-17/). El `baseCommit` de `live-87ed400.json` es `9ee2564`: el merge temporal que Actions construye para el PR, no un commit adicional de la rama.

## Decisión funcional pendiente

#150 pide que un Docente cree curso y cohorte. Los POST de CursosController y CohortesController exigen `SoloAdministrador`, y la UI oculta esos formularios al docente. Se conserva esa seguridad: el recorrido permitido es **Administrador crea curso/cohorte → Docente matricula**. El fixture real exige 403 para los dos POST del docente. El PO debe confirmar en el PR que ese recorrido satisface el criterio, o derivar el cambio de permisos a una tarea funcional. No se reabrió #150 ni se aprobó o integró el PR automáticamente.

`QA_CYPRESS_FLOW=docente` conserva la variante estricta del criterio original; falla con las políticas vigentes y no se presenta como aceptada. Por defecto se ejecuta `permisos` tanto en UI como en live.

## Reproducción desde la raíz

Requisitos: Node 20, .NET 8, Docker con motor Linux disponible y dotnet-ef 8.0.11. El CI usa Ubuntu y PostgreSQL 16; la revisión local usó Windows, Node 24.14.0 y .NET 8.0.422.

```sh
npm ci --prefix tests/contracts
npm ci --prefix tests/identity-negative
npm ci --prefix frontend
dotnet tool install --global dotnet-ef --version 8.0.11
npm test --prefix tests/contracts
dotnet build PEI.sln --nologo -m:1
dotnet test PEI.sln --no-build
npm run build --prefix frontend
npm test --prefix frontend -- --run --maxWorkers=2 --minWorkers=1
node frontend/cypress/run-ui.mjs
node tests/s2-qa01/run-api.mjs
node tests/s2-qa01/run-live.mjs
```

`run-api.mjs` usa 5543/18543. `run-live.mjs` usa 5541, 18641–18644, 8080 y 3000. Exigen puertos libres, crean bases/contenedores descartables y claves efímeras, y comprueban la etiqueta antes de limpiar el contenedor. No usan claves ni bases de desarrollo. No ejecutar QA04 y live simultáneamente (5541), ni dos runners Cypress (3000).

Para repetir en GitHub: push a la rama del PR; QA-05 ejecuta Newman/API, Cypress UI y Cypress/SignalR real. QA-06 valida contratos. CI compila frontend/backend y ejecuta backend/QA-07; QA-02 y QA-04 prueban negativos y aislamiento. El check llamado Frontend Lint & Check ejecuta el build, **no Vitest**: la suite frontend debe ejecutarse con el comando anterior. Incorporarla al CI corresponde a #237 (Sprint 3).

## Escenarios comprobados

- Contratos: ambos ejemplos de lotes contra JSON Schema draft 2020-12; negativos por obligatorios y UUID inválidos.
- Newman: 18 requests y 28 aserciones. Crear/publicar evaluaciones A/B, repetir lotes sin cambiar IDs, rechazar institución ajena y ausencia de clave interna, permitir lectura propia y rechazar listados/detalles ajenos. El runner API comprueba dos entregas en PostgreSQL después del reenvío.
- Cypress UI: MSW habilitado y comprobado; login a Inicio, menú Cursos, formularios ocultos para docente, administrador crea curso/cohorte, logout real y login docente, matrícula activa única, exclusión del selector y navegación de vuelta. No acredita E2E del backend.
- Cypress live: mismo recorrido con semillas ID-08/CU-06, JWT emitidos por Identidad y gateway/APIs reales; exige ausencia de MSW. Además recarga la página y verifica sesión, fila activa única y persistencia. Sólo se bloquean tipografías externas, no APIs.
- Permisos y duplicados reales: POST de curso/cohorte con docente → 403; matrícula repetida → 409, listado idéntico al anterior.
- SignalR real: evaluación publicada y alumno matriculado, conexión docente/alumno por gateway, inicio y reenvío del mismo payload/key → Aceptada/Duplicada, mismo timestamp a precisión PostgreSQL y una respuesta SQL.
- Cierre real: alumno no puede finalizar; docente finaliza, llega `salaFinalizada`, Evaluaciones recibe una entrega; repetir cierre conserva IDs y una fila SQL; se rechazan respuestas posteriores.

El fixture configura explícitamente `Cursos__BaseUrl=http://127.0.0.1:18642` para validar la matrícula desde Sala. La evaluación es multiple choice, sin IA. Sala usa presencia en memoria: no se certifica Redis ni su recuperación. Evaluaciones conserva CursosClientProvisorio: no se acredita validación remota de cohortes desde Evaluaciones. k6 fuera de alcance.

## Fallos investigados y correcciones

El run original [44133452468](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/actions/runs/44133452468) tenía ocho checks aprobados y dos fallidos:

1. UI esperaba `/cursos` tras el login, pero la decisión PO del 12/11 en `rutaInicial.ts` lleva todos los roles a `/`. Se comprueba Inicio y luego navegación por menú, sin sleeps.
2. Live pasó Newman y falló dentro de Cypress. El log del fixture guardaba sólo los últimos 5000 caracteres y perdió la aserción; comparte el mismo login y expectativa obsoleta. Se adapta ese recorrido y se agrega el error Cypress al final del log para no perder el diagnóstico. También se corrige la configuración del nuevo cliente de Cursos en Sala antes de ejecutar SignalR.
3. La UI ya no ofrece creación al docente. Se comprueba esa restricción y se usa administrador para crear. Los 403 se comprueban directamente contra APIs reales, sin stubs ni cambios en seguridad.
4. Cambiar sesión con `cy.visit` reinicia los datos MSW en memoria. Se usa el botón Salir y navegación SPA, que además limpia React Query y la sesión.
5. La nueva comprobación explícita de 403 expuso otro fallo del helper: `Forbid()` devuelve cuerpo vacío y `response.json()` arrojaba `Unexpected end of JSON input`. Ahora se valida siempre el status y sólo se parsea si existe cuerpo.
6. Vitest bajo Node 24.14.0 detectó incompatibilidad entre AbortSignal de jsdom y Request nativo al redirigir rutas (16 errores no manejados). Con Node 20.20.2, la versión observada en Actions, la suite completa pasó 496/496 en 68 archivos, sin cambios en tests ni funcionalidades. El timeout de matrícula tampoco se reprodujo en ese entorno.

## Evidencia

[Informe de revisión completo](REVISION-2026-11-17.md). Vitest local con Node 20.20.2: 68 archivos, 496 tests aprobados, 0 fallos y 0 omitidos; duración 230.13 s. Build frontend y backend exitosos; backend 552 aprobados y 98 omitidos, detallados en el informe.

[Run 44182609929](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/actions/runs/44182609929), commit de rama `87ed400`: Cypress UI 1/1, Cypress live 1/1, Newman 18/28, SignalR y cierre completos. `live-87ed400.json` confirma `completed=true`, `cleanedUp=true`, 403/403/409, matrícula persistida, una respuesta y una entrega SQL. Los reportes no contienen JWT ni credenciales.

Los reportes de cada ejecución se generan en `tests/s2-qa01/results/` (ignorado por Git) y Actions los publica como artifacts. No exportar environments ni reportes crudos con tokens. La copia en `evidence/` permite inspección estable de la ejecución identificada, sin presentarla como resultado de otro commit.

## Ejecuciones manuales y límites locales

La colección conserva sus carpetas y entorno existentes. Regenerar: `node tests/s2-qa01/generate.mjs`. Newman manual requiere API descartable (`QA_EVALUACIONES_URL`), `QA_INTERNAL_API_KEY`, `QA_TOKEN_A` y `QA_TOKEN_B`. `QA_TEST_VISIBILITY=false` es parcial y no satisface aceptación completa.

Cypress manual necesita Vite con `VITE_USE_MOCKS=false`, gateway 8080, `QA_DOCENTE_PASSWORD`, opcional `QA_DOCENTE_EMAIL`, semillas canónicas y nombres S2-QA-01 sin usar. Ejecutar `npm run test:e2e --prefix frontend`. El runner live garantiza bases nuevas y credenciales efímeras.

SignalR aislado: `node tests/s2-qa01/signalr-idempotencia.mjs`, con `QA_ALUMNO_TOKEN`, `QA_SALA_PIN`, sala EnCurso y pregunta no respondida; opcionales `QA_HUB_URL`/`QA_RESPUESTA`. No prueba cierre: usar live para eso.

En esta revisión Docker local primero no tenía el pipe Linux; tras iniciar Docker Desktop devolvió HTTP 500 en `/version`. El intento local de Cypress abortó porque Electron nunca conectó (cero tests ejecutados). No se atribuye éxito local a esas pruebas: se ejecutaron satisfactoriamente en Actions. Para repetir localmente, primero `docker version` debe mostrar versión de servidor y Cypress debe verificar/conectar su navegador. No se deshabilitó TLS ni se modificó seguridad del sistema.
