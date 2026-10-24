# QA-04 — evidencia de ejecución

Fecha: 2026-10-24T03:40:37.359Z. Base: 853f3f6c829e20b1a02e217a388344a9ff6359c9.

| Escenario | Método / endpoint | Rol / tenant | Esperado | Obtenido | Resultado |
|---|---|---|---|---|---|
| SETUP Login Docente A | POST /api/auth/login | anónimo / sin JWT | 200 | 200 | PASS |
| SETUP Login Docente B | POST /api/auth/login | anónimo / sin JWT | 200 | 200 | PASS |
| QA04-01 A lista sólo sus cursos | GET /api/v1/cursos | Docente / 11111111-1111-1111-1111-111111111111 | 200 | 200 | PASS |
| QA04-02 A no lee curso B | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000002 | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| QA04-03 A no lista cohortes B | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000002/cohortes | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| QA04-04 A no modifica curso B | PUT /api/v1/cursos/cccccccc-0000-0000-0000-000000000002 | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| QA04-05 A no modifica cohorte B | PUT /api/v1/cursos/cccccccc-0000-0000-0000-000000000002/cohortes/44444444-4444-4444-4444-444444444444 | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| QA04-06 B no lee curso A | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001 | Docente / 22222222-2222-2222-2222-222222222222 | 404 | 404 | PASS |
| QA04-06 B no lista cohortes A | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001/cohortes | Docente / 22222222-2222-2222-2222-222222222222 | 404 | 404 | PASS |
| QA04-07 sin JWT | GET /api/v1/cursos | anónimo / sin JWT | 401 | 401 | PASS |
| QA04-08 JWT firma inválida | GET /api/v1/cursos | Docente / 11111111-1111-1111-1111-111111111111 | 401 | 401 | PASS |
| QA04-09 missingTenantToken lista vacía | GET /api/v1/cursos | Docente / ausente | 200 | 200 | PASS |
| QA04-09 missingTenantToken detalle rechazado | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001 | Docente / ausente | 404 | 404 | PASS |
| QA04-09 missingTenantToken cohorte rechazada | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/validacion | Docente / ausente | 404 | 404 | PASS |
| QA04-09 missingTenantToken matrícula rechazada | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/matriculas/bbbbbbbb-0000-0000-0002-000000000001 | Docente / ausente | 404 | 404 | PASS |
| QA04-09 missingTenantToken escritura rechazada | POST /api/v1/cursos | Docente / ausente | 400 | 400 | PASS |
| QA04-09 malformedTenantToken lista vacía | GET /api/v1/cursos | Docente / invalido | 200 | 200 | PASS |
| QA04-09 malformedTenantToken detalle rechazado | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001 | Docente / invalido | 404 | 404 | PASS |
| QA04-09 malformedTenantToken cohorte rechazada | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/validacion | Docente / invalido | 404 | 404 | PASS |
| QA04-09 malformedTenantToken matrícula rechazada | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/matriculas/bbbbbbbb-0000-0000-0002-000000000001 | Docente / invalido | 404 | 404 | PASS |
| QA04-09 malformedTenantToken escritura rechazada | POST /api/v1/cursos | Docente / invalido | 400 | 400 | PASS |
| QA04-09 emptyTenantToken lista vacía | GET /api/v1/cursos | Docente / 00000000-0000-0000-0000-000000000000 | 200 | 200 | PASS |
| QA04-09 emptyTenantToken detalle rechazado | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001 | Docente / 00000000-0000-0000-0000-000000000000 | 404 | 404 | PASS |
| QA04-09 emptyTenantToken cohorte rechazada | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/validacion | Docente / 00000000-0000-0000-0000-000000000000 | 404 | 404 | PASS |
| QA04-09 emptyTenantToken matrícula rechazada | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/matriculas/bbbbbbbb-0000-0000-0002-000000000001 | Docente / 00000000-0000-0000-0000-000000000000 | 404 | 404 | PASS |
| QA04-09 emptyTenantToken escritura rechazada | POST /api/v1/cursos | Docente / 00000000-0000-0000-0000-000000000000 | 400 | 400 | PASS |
| QA04-10 Docente A lee su curso | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001 | Docente / 11111111-1111-1111-1111-111111111111 | 200 | 200 | PASS |
| QA04-10 Docente A lee su cohorte | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000001/cohortes | Docente / 11111111-1111-1111-1111-111111111111 | 200 | 200 | PASS |
| EXTRA A no elimina curso B | DELETE /api/v1/cursos/cccccccc-0000-0000-0000-000000000002 | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| EXTRA A no elimina cohorte B | DELETE /api/v1/cursos/cccccccc-0000-0000-0000-000000000002/cohortes/44444444-4444-4444-4444-444444444444 | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| EXTRA A no crea cohorte en B | POST /api/v1/cursos/cccccccc-0000-0000-0000-000000000002/cohortes | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| EXTRA A no valida cohorte B | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/validacion | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| EXTRA A no lee matrícula B | GET /api/v1/cohortes/44444444-4444-4444-4444-444444444444/matriculas/bbbbbbbb-0000-0000-0002-000000000001 | Docente / 11111111-1111-1111-1111-111111111111 | 404 | 404 | PASS |
| EXTRA B no modifica curso A | PUT /api/v1/cursos/cccccccc-0000-0000-0000-000000000001 | Docente / 22222222-2222-2222-2222-222222222222 | 404 | 404 | PASS |
| EXTRA B conserva curso después de ataques | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000002 | Docente / 22222222-2222-2222-2222-222222222222 | 200 | 200 | PASS |
| EXTRA B conserva cohorte después de ataques | GET /api/v1/cursos/cccccccc-0000-0000-0000-000000000002/cohortes | Docente / 22222222-2222-2222-2222-222222222222 | 200 | 200 | PASS |
| EXTRA encabezado B no suplanta JWT A | GET /api/v1/cursos | Docente / 11111111-1111-1111-1111-111111111111 | 200 | 200 | PASS |
| EXTRA rol Docente validado por Identidad | GET /api/auth/docente | Docente / 11111111-1111-1111-1111-111111111111 | 200 | 200 | PASS |
| EXTRA Docente no tiene acceso Administrador | GET /api/auth/administrador | Docente / 11111111-1111-1111-1111-111111111111 | 403 | 403 | PASS |
