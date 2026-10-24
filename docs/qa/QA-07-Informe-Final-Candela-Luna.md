# QA-07 — Informe final — Candela Luna

Fecha: 24/10/2026. Equipo: 404 Found. Rama: `feature/QA-07-test-concurrencia`.

## 1. Resumen ejecutivo

Se implementó una prueba reproducible de publicación concurrente con PostgreSQL real
en Docker. Se reprodujo un defecto que devolvía HTTP 500 cuando EF Core detectaba una
colisión y se corrigió su traducción a HTTP 409. Después del arreglo, 40 carreras
independientes devolvieron exactamente un 200 y un 409, sin 500, con estado Publicada,
versión 2 y contenido preservado. La colisión determinista también pasó.

## 2. Estado inicial

Árbol limpio en `feature/QA-02-tests-identidad`. Se hizo `git fetch origin`, se revisó
el historial y se actualizó main mediante fast-forward (ya estaba al día). QA-07 se
creó desde `origin/main`, commit `853f3f6`, sin incorporar QA-02/04/06.
GitHub REST confirmó abiertos #106, #107 y #108; EV-04 #83 y EV-08 #66 ya estaban
fusionados. No existía rama QA-07 local/remota. El conector GitHub no estuvo disponible
y `gh` no estaba instalado; se usó la API REST como alternativa.

Existían controlador POST publicar, entidad, Version con `IsConcurrencyToken()`,
repositorio Npgsql, dos migraciones, semillas y Compose PostgreSQL 16. Los scripts
usaban PATCH, gateway 8080, campos incorrectos y Bearer ficticio, sin aserciones.
El perfil API usaba 5104. El repositorio no traducía `DbUpdateConcurrencyException`.
La suite unitaria existente tenía solamente un test vacío.

## 3. Infraestructura Docker

Docker Desktop 4.96.2, Engine 29.9.2, Compose 5.6.1. SDK 8.0.422 compatible con
`global.json`; EF CLI 8.0.30 y provider/proyecto EF 8.0.11.
Antes del trabajo solo existían contenedores detenidos de Identidad/Redis y sus
volúmenes. 5443 y 5103 estaban libres. Se ejecutó únicamente:

```text
docker compose -f infra/docker-compose.yml config --quiet
docker compose -f infra/docker-compose.yml ps
docker compose -f infra/docker-compose.yml up -d --wait postgres-evaluaciones
docker exec pei-db-evaluaciones createdb -U postgres pei_evaluaciones_qa07
```

Contenedor `pei-db-evaluaciones`, imagen postgres:16-alpine, PostgreSQL 16.15, puerto
5443, volumen nuevo `infra_db_evaluaciones_data`. Se verificaron healthcheck healthy,
`current_database() = pei_evaluaciones_qa07`, versión y cero tablas públicas antes
de aplicar migraciones. No se ejecutaron migraciones en `pei_evaluaciones_db`.
La contraseña se generó aleatoriamente y se guardó solo en configuración local ignorada.

API local con `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://127.0.0.1:5103`,
`ConnectionStrings__Default` exclusivo QA y `--no-launch-profile`. Swagger respondió
200; las semillas se insertaron en QA y no fueron utilizadas por las carreras.

## 4. Cambios

- `EvaluacionRepositorio.cs`: captura exclusivamente el conflicto EF, desvincula las
  entradas perdedoras y lo traduce a una excepción de Application.
- `ConflictoConcurrenciaException.cs`: frontera sin dependencia EF en Application.
- `PublicarEvaluacion.cs`: convierte esa excepción en ResultadoOperacion.Conflicto;
  el controlador existente responde 409. No hay reintento ni cambio de regla de negocio.
- `PublicarConcurrenciaTests.cs`: dos tests unitarios que comprueban traducción sin
  reintento y propagación de errores no relacionados.
- Proyecto `PEI.Evaluaciones.IntegrationTests` y alta en solución: test determinista
  Npgsql con dos contextos y barrera posterior a las lecturas de versión 1.
- `tests/concurrencia/Runner/{QA07.Runner.csproj,Program.cs,QaSupport.cs}`: motor común,
  validaciones de destino/estado, creación por API, barrera HTTP, SQL y evidencias.
- `test-concurrencia.ps1`, `test-concurrencia.sh`, `tests/concurrencia/README.md`:
  wrappers configurables y procedimiento seguro de reproducción.
- `.github/workflows/ci.yml`: job QA-07 independiente con Docker efímero del runner,
  secreto aleatorio enmascarado, migraciones, xUnit, API, 30 carreras y artefactos.
- Casos, este informe y `docs/qa/evidencias/QA-07/`: evidencia sanitizada antes/después.

La corrección toca código de producción de Evaluaciones y debe revisarla su responsable
(EV-04). No se cambiaron contratos, migraciones, stub de Cursos ni reglas de publicación.

## 5. Pruebas ejecutadas realmente

```text
dotnet ef migrations list --project services/evaluaciones/src/PEI.Evaluaciones.Infrastructure --startup-project services/evaluaciones/src/PEI.Evaluaciones.Api
dotnet ef database update --project services/evaluaciones/src/PEI.Evaluaciones.Infrastructure --startup-project services/evaluaciones/src/PEI.Evaluaciones.Api
dotnet build services/evaluaciones/PEI.Evaluaciones.sln
dotnet test services/evaluaciones/PEI.Evaluaciones.sln --logger trx --results-directory TestResults/final
./tests/concurrencia/test-concurrencia.ps1 -Repetitions 30 -Output TestResults/qa07-after-http.json
bash tests/concurrencia/test-concurrencia.sh --repetitions 10 --output TestResults/qa07-bash-http.json
git diff --check
```

Build PASS: cero errores/advertencias. xUnit final PASS: 3 unitarios (incluye el
placeholder original) y 1 integración real. Antes del arreglo: integración FAIL
por excepción EF; 10 carreras HTTP, 1 PASS y 9 FAIL con 200/500. Después: PowerShell
30/30 PASS y Bash de Git para Windows 10/10 PASS. Guardas de base ajena y registro
publicado rechazaron con salida 1 según lo esperado. Sin regresiones observadas en
Evaluaciones; no se afirma cobertura de todos los microservicios.

## 6. Concurrencia y persistencia

Cada carrera crea un UUID por POST y comprueba Borrador v1 en API/SQL. Dos solicitudes
idénticas se liberan desde una barrera, usando el encabezado institucional real.
Se conservan códigos/cuerpos A y B. PostgreSQL confirma única fila, Publicada v2,
cohorte solicitada, preguntas/título/tipo/institución/fecha de creación intactos.
Un tercer request obsoleto recibe 409 sin modificar la fecha de actualización.

El test determinista sincroniza dos handlers después de leer versión 1, por lo que
exige que el token EF resuelva una colisión real de persistencia. No depende del
azar del scheduler HTTP. Una publicación se refiere a una transición persistida;
el modelo no tiene tabla de auditoría ni prueba de eventos externos exactly-once.

## 7. Defectos

Defecto reproducido: el chequeo inicial de versión no impide que ambos handlers lean
v1. EF ejecuta UPDATE condicionado por Version y el perdedor afecta cero filas;
la excepción sin capturar escapaba hasta ASP.NET (500). Corrección acotada y test
de regresión descritos arriba. Las evidencias históricas FAIL se conservan.

## 8. Seguridad

No se usaron bases productivas ni datos de compañeros. No se eliminaron contenedores
ajenos ni volúmenes. Configuración/credenciales y logs locales quedan ignorados;
los JSON publicados contienen datos sintéticos y omiten cuerpos 500. La base QA
conserva los registros de evidencia. No se cambió el valor esperado para conseguir PASS.
Durante el trabajo apareció `tests/identity-negative/` como directorio no versionado;
se preservó y excluyó de QA-07.

## 9. GitHub Actions y pendientes

PR: https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/pull/109.
Commit de implementación: `47641b1`. Ejecución remota verificada:
https://github.com/Proyecto-Software-Equipo-404-FOUND/Tiza/actions/runs/40115871072.
Los tres jobs finalizaron **success**: Backend Build & Test, Frontend Lint & Check
y QA-07 PostgreSQL y concurrencia 200/409. Se descargó el artefacto `qa07-evidencias`
y se verificaron 30 carreras adicionales sin fallos, 3 unitarios y 1 integración
aprobados en Ubuntu. Evidencia: `qa07-ci-http.json` y `ci-xunit.json`.
Total posterior al arreglo: **70/70 carreras HTTP PASS** (40 locales + 30 CI).
También se comprobó localmente la omisión explícita del test de integración al
retirar `QA07_CONNECTION`: 0 aprobados, 1 SKIPPED, nunca un falso PASS.

Cursos sigue siendo provisional: su integración real está BLOCKED para este alcance.
El perfil original sigue en 5104; el procedimiento y CI fijan 5103 explícitamente.
Se requiere revisión del responsable de Evaluaciones
y de Candela antes de fusionar. No se hará merge automático.

## 10. Conclusión

Implementación y validación local de QA-07 completas: PostgreSQL Docker, 200/409,
única transición, consistencia, ausencia de 500 después del arreglo y colisión
determinista verificadas. La aprobación del equipo y los checks remotos son el cierre
de integración del PR; los checks del commit de implementación ya están aprobados.
La validación de pertenencia de cohorte en Cursos es una
dependencia externa que esta prueba no pretende dar por terminada.
