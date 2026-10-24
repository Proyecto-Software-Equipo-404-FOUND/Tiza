# QA-02 — Informe final — Candela Luna

Fecha: 24/10/2026. Sprint 1. Fecha límite original: 20/10/2026.
Rama: `feature/QA-02-tests-identidad`. Base inspeccionada: `853f3f6` de `origin/main`.

## Resumen ejecutivo

Se automatizaron 33 escenarios de autenticación, permisos, validación y controles positivos sobre la API real de Identidad mediante Newman y TestServer. Se agregaron 17 casos HTTP xUnit de validación, reutilizando fábricas y persistencia en memoria existentes. No se modificó código de producción ni la seguridad, y no se agregaron endpoints de prueba a la API productiva. La ejecución final local aprobó 251 tests .NET (186 unitarios + 65 de integración), 33 casos Postman y 141 aserciones Newman.

## Estado inicial y coordinación

Se verificaron estado Git, rama, remotos, fetch, últimos commits, ramas locales/remotas y PR abiertos/cerrados. El checkout estaba limpio. `main` era la rama principal; `git pull --ff-only origin main` no requirió cambios. No se encontró una rama o PR previo de QA-02 ni QA-05 en la búsqueda realizada. Se creó la rama solicitada desde main, sin incorporar las ramas QA-04/QA-06.

La suite inicial aprobó 169 tests unitarios. Ya había pruebas HTTP de JWT, roles, validaciones básicas, OpenAPI y separación institucional/Plataforma. La matriz inicial y los contratos revisados están en [Casos de prueba](QA-02-Casos-de-Prueba.md). Se conservaron esas pruebas y se ampliaron las aserciones que faltaban.

Se inspeccionaron los archivos de los PR abiertos: #85 y #86 incluyen `AuthenticationHttpTests.cs`; ese archivo no se modificó. No se detectó coincidencia con los archivos finalmente editados de QA-02 en los PR inspeccionados. Esta comprobación no permite conocer trabajo local no publicado de otros integrantes. No se modificaron QA-03, QA-04 ni QA-06.

## Cambios por archivo

| Archivo | Cambio y propósito |
|---|---|
| `services/identidad/tests/PEI.Identidad.UnitTests/Qa02Fixture.cs` | Reutiliza PlatformHttpFactory, sus claves JWT y fakes; crea usuarios y credenciales efímeras; sustituye también el registro por memoria. |
| `services/identidad/tests/PEI.Identidad.UnitTests/Qa02ValidationTests.cs` | 17 casos HTTP de 400 con errors, ausencia de secretos y ausencia de escrituras. |
| `services/identidad/tests/PEI.Identidad.UnitTests/PEI.Identidad.UnitTests.csproj` | Copia los casos compartidos al output de tests. |
| `services/identidad/tests/PEI.Identidad.Qa02Host/PEI.Identidad.Qa02Host.csproj` y `Qa02Host.cs` | Puente local de Newman hacia TestServer; no forma parte de la solución publicada ni usa una base real. |
| `tests/identity-negative/generate.mjs` y `cases.json` | Fuente reproducible de 33 casos compartidos entre Postman y xUnit. |
| `tests/identity-negative/run.mjs` | Compila, ejecuta xUnit, inicia fixture, verifica guardas y Newman, comprueba contadores y genera evidencia sanitizada. |
| `tests/identity-negative/postgres.mjs` | Regresión opcional de 65 tests existentes sobre PostgreSQL descartable, con limpieza limitada al contenedor propio. |
| `tests/identity-negative/package.json`, `package-lock.json`, `.gitignore` | Newman fijado y dependencias reproducibles; excluye instalaciones y resultados locales. |
| `tests/identity-negative/README.md` | Comandos, requisitos, arquitectura, guardas, privacidad y limitaciones. |
| `tests/postman/identidad.postman_collection.json` | Añade sólo la carpeta QA-02; conserva semánticamente todo el contenido previo. |
| `.github/workflows/qa02-identidad.yml` | Ejecuta xUnit y Newman en el runner, sin credenciales ni servicios externos; publica sólo summary sanitizado. |
| `docs/qa/QA-02-Casos-de-Prueba.md` y este informe | Matrices, resultados, contratos, hallazgos y revisión. |
| `docs/qa/evidencias/qa02/*.json` | Resultado final, regresión PostgreSQL y primer intento Newman fallido, sin tokens ni cuerpos. |

El environment Postman original y `.github/workflows/ci.yml` permanecen sin cambios. La integración específica puede reutilizarse para QA-05 sin atribuirle una implementación que no se encontró.

## Pruebas ejecutadas y evidencias

| Verificación final local | PASS | FAIL | SKIPPED | BLOCKED |
|---|---:|---:|---:|---:|
| xUnit Identidad (169 existentes + 17 nuevos) | 186 | 0 | 0 | 0 |
| Integración PostgreSQL existente | 65 | 0 | 0 | 0 |
| Casos Postman QA-02 | 33 | 0 | 0 | 0 |
| Aserciones Newman (dentro de los 33 casos; no sumar como casos distintos) | 141 | 0 | 0 | 0 |
| Guardas del runner | 2 | 0 | 0 | 0 |

Las guardas ejecutadas prueban: sin fixture autorizado, cero solicitudes; con fixture y mutaciones deshabilitadas, sólo 12 GET y ningún POST. Las omisiones deliberadas de estas corridas prueban la guarda y no son casos de aceptación pendientes. El store conservó tres usuarios y cero altas de Plataforma antes y después.

Comandos ejecutados: `dotnet build services/identidad/PEI.Identidad.sln`, `dotnet test services/identidad/tests/PEI.Identidad.UnitTests/PEI.Identidad.UnitTests.csproj`, el build del host, `node tests/identity-negative/run.mjs`, `node tests/identity-negative/postgres.mjs`, generación/validación JSON y `git diff --check`. El runner vuelve a compilar y ejecuta el proyecto unitario por su directorio. Se utilizaron .NET 8, Node 20, Newman 6.2.1 y PostgreSQL 16 Alpine.

La regresión PostgreSQL usó `pei_identidad_tests`, puerto local 5541 y un contenedor con UUID y etiqueta propia. La evidencia confirma su eliminación con el volumen temporal; no se reutilizaron bases de desarrollo. La CI QA-02 no ejecuta esta regresión opcional de Docker.

Evidencias: [ejecución final](evidencias/qa02/resultado-final.json), [PostgreSQL](evidencias/qa02/postgres-summary.json), [primer intento fallido](evidencias/qa02/primer-intento-newman.json). Los contadores son resultados reales, no estimaciones. Las marcas de tiempo JSON están en UTC; algunos ensayos comenzaron durante la noche anterior en Argentina.

## Incidencias y defectos

| Hallazgo | Severidad | Estado / explicación |
|---|---|---|
| Primer intento Newman: QA02-07 no obtuvo respuesta; cuatro aserciones fallaron y hubo un fallo adicional de transporte | Media, infraestructura de prueba | Conservado como FAIL histórico. Ocurrió durante ejecución concurrente con PostgreSQL y poca memoria. No hay evidencia suficiente para atribuir una causa única. Se separaron suites y se amplió timeout por solicitud de 10 a 30 segundos; la repetición completa pasó sin cambiar el 403 esperado. |
| Arranque inicial del puente fuera del runner xUnit | Baja, harness de prueba | Resuelto: ContentRoot explícito y proveedor de configuración del host después de limpiar sus fuentes. Los intentos anteriores no ejecutaron Newman y no se contabilizan como PASS. |
| Plataforma devuelve 401 application/json ante token institucional | Informativa | Conforme al esquema y contrato; no se fuerza 403 ni otro Content-Type. |
| Rol ausente y logout vacío | Informativa | Rol ausente devuelve 401; logout vacío 204. Son comportamientos efectivos, no defectos ni reglas inventadas. |

Defectos de producción abiertos detectados por QA-02: **0**. No se afirma ausencia universal de vulnerabilidades: esta tarea verifica los escenarios y contratos enumerados. Los campos opcionales `type/detail/instance` se comprueban sólo cuando existen; la validación de DTO exige `errors` y status coherente.

## Cobertura, dependencias y resultado

Los seis criterios iniciales de aceptación se verificaron satisfactoriamente: credenciales inválidas, acceso anónimo, JWT inválido, permisos insuficientes, solicitudes inválidas y contrato de errores. **6/6 = 100 % de criterios QA-02**; **33/33 = 100 % de escenarios planificados**. Esto no es un porcentaje de cobertura de líneas de código ni de todos los ataques posibles.

La suite principal requiere checkout completo, .NET 8, Node 20 y puerto local 18502 disponible; no necesita cuentas, PostgreSQL ni Docker. La regresión adicional requiere Docker Linux y puerto 5541 disponible. Ninguna dependencia externa bloquea los criterios de aceptación.

Resultado: **COMPLETADA**. Rama publicada y [PR #108](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/108) abierto como borrador, sin merge. El commit de implementación `8a9eed0` aprobó los tres checks: [backend y frontend](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/actions/runs/40108117387) y [QA-02 con Newman](https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/actions/runs/40108117442). El último ajuste compara la colección contra el ancestro común con origin/main en lugar de una revisión fija y volvió a aprobar 186 tests y 33 casos / 141 aserciones localmente. Los checks del último commit se consultan en el PR antes del merge.

Antes del merge, Candela Luna debería revisar las expectativas 401/403 entre esquemas, los límites reales de DTO, la carpeta agregada sin alterar las anteriores, el aislamiento del host de testing, los resultados finales del PR y la coordinación con quienes mantienen #85/#86 y QA-05. Nunca habilitar las variables del fixture sobre una base persistente.
