# QA-07: publicación concurrente sobre PostgreSQL Docker

Los wrappers PowerShell y Bash ejecutan el mismo motor .NET 8 (`Runner`). No requieren
curl, jq ni tokens Bearer. El contrato real es `POST /api/v1/evaluaciones/{id}/publicar`,
encabezado `X-Institucion-Id`, cuerpo `{"versionEsperada":1,"cohorteId":"…"}`.

## Preparar el entorno de forma segura

Desde la raíz del repositorio:

1. Verificar `git status`, `docker version`, `docker compose version`, `dotnet --version`
   y `dotnet ef --version`. SDK 8 según `global.json`; EF CLI 8.
2. Ejecutar `docker compose -f infra/docker-compose.yml config --quiet` y
   `docker compose -f infra/docker-compose.yml ps`. Revisar contenedores, volúmenes y
   ocupación de 5443/5103. No detener instancias ajenas. Ante conflicto, usar un proyecto
   Compose aislado con nombre, volumen y puerto propios y adaptar la conexión.
3. Si no existe PostgreSQL de Evaluaciones, definir `POSTGRES_PASSWORD` con un secreto
   local generado y ejecutar
   `docker compose -f infra/docker-compose.yml up -d --wait postgres-evaluaciones`.
   Si ya existe, usar sus credenciales autorizadas; cambiar la variable NO cambia la
   contraseña de un volumen existente.
4. Comprobar `docker inspect --format '{{.State.Health.Status}}' pei-db-evaluaciones`
   y consultar `SELECT datname FROM pg_database` mediante `docker exec ... psql`.
   Crear **solo si no existe**: `docker exec pei-db-evaluaciones createdb -U postgres pei_evaluaciones_qa07`.
   Si existe, verificar su procedencia y contenido antes de reutilizarla.
5. Verificar destino y tablas antes de migrar:

   ```powershell
   docker exec pei-db-evaluaciones psql -U postgres -d pei_evaluaciones_qa07 -Atc 'SELECT current_database(); SELECT count(*) FROM information_schema.tables WHERE table_schema = ''public'''
   ```

6. Configurar `QA07_CONNECTION` sin versionarla ni imprimirla. Formato:
   `Host=127.0.0.1;Port=5443;Database=pei_evaluaciones_qa07;Username=<usuario>;Password=<secreto local>`.
   Usar variables de entorno o un archivo `.env.*` ignorado. Nunca pasar secretos como argumentos.

En PowerShell, una vez verificado el destino:

```powershell
$env:ConnectionStrings__Default = $env:QA07_CONNECTION
dotnet ef migrations list --project services/evaluaciones/src/PEI.Evaluaciones.Infrastructure --startup-project services/evaluaciones/src/PEI.Evaluaciones.Api
dotnet ef database update --project services/evaluaciones/src/PEI.Evaluaciones.Infrastructure --startup-project services/evaluaciones/src/PEI.Evaluaciones.Api
dotnet build services/evaluaciones/PEI.Evaluaciones.sln
dotnet test services/evaluaciones/PEI.Evaluaciones.sln
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5103'
dotnet run --no-launch-profile --project services/evaluaciones/src/PEI.Evaluaciones.Api
```

Usar otra terminal con `QA07_CONNECTION` para ejecutar las pruebas. `--no-launch-profile`
evita el perfil existente que usa 5104 (reservado para Sala en Vivo).
Comprobar HTTP 200 de `/swagger/v1/swagger.json` antes de continuar. Development inserta
sus dos semillas únicamente en la base QA; las pruebas crean sus propias evaluaciones.
No ejecutar borrados de volúmenes ni reinicializar bases. Los registros QA se conservan
para inspección; cada ejecución crea UUID nuevos y no necesita limpiar ni restablecer datos.

## Ejecutar

```powershell
./tests/concurrencia/test-concurrencia.ps1 -Url http://127.0.0.1:5103 -Database pei_evaluaciones_qa07 -Repetitions 30 -Output TestResults/qa07-http.json
```

```bash
# QA07_CONNECTION debe estar exportada; para iniciar la API exportar también
# ConnectionStrings__Default="$QA07_CONNECTION", ASPNETCORE_ENVIRONMENT y ASPNETCORE_URLS.
bash tests/concurrencia/test-concurrencia.sh --url http://127.0.0.1:5103 --database pei_evaluaciones_qa07 --repetitions 30 --output TestResults/qa07-http.json
```

| PowerShell | Bash | Valor predeterminado |
|---|---|---|
| `-Url` | `--url` | `http://127.0.0.1:5103` |
| `-Database` | `--database` | `pei_evaluaciones_qa07` |
| `-Institution` | `--institution` | `70707070-7070-4070-8070-707070707070` |
| `-Cohort` | `--cohort` | `33333333-3333-3333-3333-333333333333` |
| `-Repetitions` | `--repetitions` | 10 |
| `-Id` | `--id` | Crear una evaluación nueva por repetición |
| `-Output` | `--output` | `TestResults/qa07-http.json` |

Con ID explícito usar una única repetición, título prefijado `QA-07 `, institución
indicada, Borrador, versión 1 y cohorte nula. El motor rechaza bases cuyo nombre no
comience por `pei_evaluaciones_qa07` o no coincida con la conexión. La URL debe apuntar
a la API configurada con esa misma base; no usar un gateway ni una API compartida.

## Aserciones y alcance

- Preflight: conexión efectiva, migraciones aplicadas, API accesible y registro inicial.
- Dos tareas HTTP bloqueadas en una barrera y liberadas juntas; respuestas A/B separadas.
- Exactamente un 200 y un 409; 204, dos éxitos, dos conflictos, 500 o errores de red fallan.
- Lectura nueva en PostgreSQL: una fila por PK, Publicada, versión 2, cohorte correcta,
  mismo título, tipo, institución, preguntas y fecha de creación.
- Un tercer request es un reintento obsoleto de control: exige 409 y ninguna alteración
  de la fecha de actualización ni del contenido. No cuenta como parte de la carrera.
- JSON de evidencias por repetición y contador total de fallos. Código de salida 1
  si falla una repetición o el preflight; 0 solamente si todas pasan.
- No se guardan cuerpos inesperados/500 para evitar publicar trazas del servidor.

La simultaneidad HTTP no demuestra por sí sola que ambos handlers leyeron versión 1.
Por eso `PublicacionConcurrenteTests` usa dos DbContext reales Npgsql y una barrera en
el cliente de Cursos de prueba, después de leer/validar versión y antes de guardar.
Sin el arreglo falla con `DbUpdateConcurrencyException`; con él exige un único ganador.
No sustituye PostgreSQL por EF InMemory. Sin `QA07_CONNECTION` este test se marca
SKIPPED con motivo BLOCKED; nunca PASS. El job específico de CI siempre configura la conexión.

No hay tabla de auditoría de publicaciones: «una publicación» se verifica mediante
una única respuesta exitosa y una única transición persistida 1→2. No se afirma
exactly-once de eventos externos. `CursosClientProvisorio` sigue devolviendo true;
la cohorte es sintética y esto no valida integración real con Cursos.

## CI y evidencias

El job `qa07-concurrencia` de `.github/workflows/ci.yml` utiliza Docker del runner Ubuntu,
contraseña aleatoria efímera enmascarada, base exclusiva, migraciones, xUnit y 30 carreras
mediante Bash. Publica JSON/TRX incluso ante fallos. No requiere Docker Desktop remoto.
El job general conserva sus pasos; allí la integración se omite si no hay conexión QA.

Resultados locales sanitizados: `docs/qa/evidencias/QA-07/`.
Casos: `docs/qa/QA-07-Casos-de-Prueba.md`.
Informe: `docs/qa/QA-07-Informe-Final-Candela-Luna.md`.
