# QA-07 — Casos de prueba

Responsable: Candela Luna. Ejecución local: 24/10/2026.
Base: `pei_evaluaciones_qa07`, PostgreSQL 16.15 en Docker, API .NET 8 en 5103.
Precondiciones comunes: migraciones aplicadas, conexión QA verificada, institución
`70707070-7070-4070-8070-707070707070`, cohorte sintética
`33333333-3333-3333-3333-333333333333`, UUID de evaluación nuevo por carrera,
título `QA-07 <UUID>`, pregunta Abierta con rúbrica válida según JSON Schema.

| ID | Objetivo / método | Esperado | Obtenido | Estado | Evidencia |
|---|---|---|---|---|---|
| QA07-01 | Docker healthcheck y SQL `current_database()` | Healthy, base exclusiva vacía antes de migrar | Healthy, PostgreSQL 16.15, 0 tablas públicas iniciales | PASS | Informe, comandos de infraestructura |
| QA07-02 | Build solución Evaluaciones | Sin errores | 0 errores, 0 advertencias | PASS | Informe |
| QA07-03 | GET Swagger y GET evaluaciones con encabezado institucional | 200 | 200 | PASS | Preflight ejecutado por runner |
| QA07-04 | POST `/api/v1/evaluaciones` | 201; Borrador v1; preguntas válidas | Una evaluación nueva por repetición | PASS | JSON HTTP y UUID de cada carrera |
| QA07-05 | Dos POST `/api/v1/evaluaciones/{id}/publicar` antes del arreglo | Un 200 y un 409 | 1/10 cumple; 9/10 devuelven 200/500 | FAIL histórico | `qa07-before-http.json` |
| QA07-06 | Dos handlers con lecturas v1 sincronizadas antes del arreglo | Ok + Conflicto | Excepción EF no controlada | FAIL histórico | `xunit.json`, fase anterior |
| QA07-07 | Mismos dos POST después del arreglo, PowerShell | 200/409, sin 500 | 30/30 cumplen | PASS | `qa07-after-http.json` |
| QA07-08 | Mismos dos POST, Bash de Git para Windows | 200/409, sin 500 | 10/10 cumplen | PASS | `qa07-bash-http.json` |
| QA07-09 | SELECT independiente al finalizar cada carrera | Publicada v2, cohorte correcta, contenido sin cambios | 40/40 cumplen | PASS | JSON HTTP, campos finales |
| QA07-10 | Tercer POST con versión 1 ya obsoleta | 409 y sin cambios en DB | 40/40 cumplen, fecha idéntica | PASS | `retryStatus` y aserciones del runner |
| QA07-11 | Test determinista con dos DbContext PostgreSQL, después | Un Ok, un Conflicto, única transición 1→2 | Cumple | PASS | `xunit.json`, fase posterior |
| QA07-12 | Tests unitarios: conflicto sin reintento / otro error no ocultado | Conflicto / propagar error original | Ambos pasan; también pasa el placeholder preexistente | PASS | `xunit.json` |
| QA07-13 | Runner con base no QA | Salida 1 antes de escritura | BLOCKED y salida 1, rechazo esperado | PASS | `qa07-reject-database.json` |
| QA07-14 | Runner con UUID ya publicado | Salida 1 sin publicar otra vez | FAIL de precondición y salida 1, rechazo esperado | PASS | `qa07-reject-published.json` |
| QA07-15 | Integración real con Cursos | Servicio real valide pertenencia | Cliente provisional sigue devolviendo true | BLOCKED | `CursosClientProvisorio.cs` |
| QA07-16 | Job QA-07 en GitHub Actions Ubuntu | Docker, xUnit y HTTP reales aprobados | 30/30 carreras, 3 unitarios y 1 integración PASS | PASS | `qa07-ci-http.json`, `ci-xunit.json`, run 40115871072 |
| QA07-17 | Integración sin QA07_CONNECTION | Omisión explícita, no falso PASS | 0 aprobados, 1 omitido con motivo BLOCKED | PASS (guarda); integración SKIPPED | TRX local `TestResults/qa07-without-db.trx` |

Los JSON están en `docs/qa/evidencias/QA-07/`. Un FAIL/BLOCKED dentro de la evidencia
de las guardas QA07-13/14 es intencional: el caso exige ese rechazo. Los FAIL anteriores
al arreglo se conservan y no se mezclan con la validación final.

Reproducción: seguir `tests/concurrencia/README.md`. Para reproducir el defecto anterior,
aplicar los tests a `853f3f6` sin la corrección de producción, siempre con base QA aislada.
El estado de GitHub Actions se registra en el informe final y en el PR; no se infiere
su resultado a partir de las ejecuciones locales.
