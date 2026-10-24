# ID-09 — Configuración de desarrollo de Identidad

## 1. Alcance y requisitos

Desde un clon del repositorio, esta guía permite preparar PostgreSQL, configurar la conexión y
JWT, revisar/aplicar las migraciones existentes y ejecutar la API directamente. No crea usuarios,
instituciones ni datos semilla. ID-08 es un trabajo separado y no se presupone integrado.

Requisitos: .NET 8 SDK compatible con `global.json` (8.0.401 con rollForward latestFeature),
Docker Desktop con motor Linux y Compose v2, PowerShell 7 y, para verificar PostgreSQL desde
el host, cliente `psql`. Node.js no es necesario para levantar solamente Identidad.
Todos los comandos parten de la **raíz del repositorio** y se ejecutan manualmente.

```powershell
dotnet --version
docker version
docker compose version
psql --version
dotnet ef --version
```

EF Core y Npgsql del proyecto usan 8.0.4. Si falta la herramienta EF, instalar explícitamente
la versión compatible; no instalar automáticamente la versión mayor más reciente:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.4
```

Si ya existe, comprobar su versión antes de decidir actualizarla. No hace falta agregar paquetes
ni ejecutar `dotnet user-secrets init`: la API ya tiene UserSecretsId.

## 2. PostgreSQL existente en Docker

El Compose versionado define `postgres-identidad`, contenedor `pei-db-identidad`, imagen
`postgres:16-alpine`, base `pei_identidad_db`, puerto del host **5441** hacia **5432** del
contenedor y un volumen persistente. No crear otro contenedor ni otra base para seguir esta guía.

Si falta el archivo local de configuración, copiar la plantilla sin sobrescribir uno existente:

```powershell
if (-not (Test-Path -LiteralPath .\infra\.env)) {
    Copy-Item -LiteralPath .\infra\.env.example -Destination .\infra\.env
}
```

Revisar privadamente `infra/.env` antes de arrancar. Está ignorado por Git; contiene configuración
local, no debe compartirse. Las credenciales de la plantilla son ejemplos, no una recomendación
para producción. El recorrido básico conserva el usuario previsto por el Compose; su healthcheck
usa `pg_isready -U postgres`. Si se cambia POSTGRES_USER, revisar también ese healthcheck.
Variables POSTGRES_USER/POSTGRES_PASSWORD heredadas del proceso tienen precedencia sobre el archivo.

**Antes de ejecutar up**, comprobar si ya existe el contenedor y su publicación real:

```powershell
docker ps -a --filter name=pei-db-identidad --format '{{.Names}} {{.Ports}} {{.Status}}'
docker port pei-db-identidad 5432/tcp
```

En un clon nuevo el segundo comando puede fallar porque aún no existe el contenedor. Si ya existe
y publica **5541** u otro puerto, no ejecutar up para recrearlo/cambiarlo automáticamente:
confirmar su propietario y destino, y acordar la configuración local. No cambiar el Compose
compartido ni reutilizar datos ajenos para hacer coincidir esta guía.

Para un entorno nuevo o existente cuya configuración y datos hayan sido autorizados:

```powershell
docker compose --env-file .\infra\.env -f .\infra\docker-compose.yml up -d postgres-identidad
docker compose --env-file .\infra\.env -f .\infra\docker-compose.yml ps postgres-identidad
docker port pei-db-identidad 5432/tcp
docker exec pei-db-identidad pg_isready -U postgres -d pei_identidad_db
```

Modificar POSTGRES_PASSWORD en un archivo no cambia la contraseña de un rol ya creado en el
volumen. Ante un fallo de autenticación, verificar la credencial vigente con su responsable.
No borrar volúmenes, ejecutar `down -v` ni reinicializar la base para resolverlo.

## 3. Configuración local y secretos

> **Clave JWT compartida (ID-11).** En desarrollo, `Jwt:SecretKey` ya viene en
> `appsettings.Development.json` y es **la misma en todos los servicios** (Identidad, Cursos y los
> que se sumen). Así los demás servicios pueden validar los tokens que emite Identidad.
> **No generes una clave propia para `Jwt:SecretKey`.** Si ya cargaste una con User Secrets,
> borrala, porque User Secrets pisa al archivo y Cursos rechazaría tus tokens con 401:
>
> `dotnet user-secrets remove Jwt:SecretKey --project .\services\identidad\src\PEI.Identidad.Api`
>
> Esa clave es **solo de desarrollo**: en cualquier otro entorno se configura con una clave
> propia, fuera del repositorio. La cadena de conexión sigue configurándose como indica esta sección.

La API lee `ConnectionStrings:IdentidadDb` y `Jwt:SecretKey`. En variables de entorno se escriben
`ConnectionStrings__IdentidadDb` y `Jwt__SecretKey`. No usar el nombre antiguo `Jwt:Secret`.
Las variables del proceso prevalecen sobre User Secrets y appsettings; revisar valores heredados
sin imprimirlos. La API carga User Secrets en Development; son almacenamiento local **sin cifrar**,
no una bóveda. Restringir el acceso al perfil del usuario y no ejecutar `user-secrets list` directamente
en una sesión grabada o compartida.

Preparar estas variables en una sesión privada. La contraseña se solicita sin eco; la conexión se
construye con un builder para soportar caracteres especiales sin concatenarlos en SQL o argumentos:

```powershell
$api = '.\services\identidad\src\PEI.Identidad.Api'
$infra = '.\services\identidad\src\PEI.Identidad.Infrastructure'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Platform__Enabled = 'false'
$env:PlatformBootstrap__Enabled = 'false'
$env:DevelopmentTools__EnableTestTokens = 'false'

$dbPort = 5441 # Debe coincidir con la publicación verificada y autorizada del contenedor.
$dbUser = Read-Host 'Usuario PostgreSQL local autorizado'
$securePassword = Read-Host 'Contraseña PostgreSQL local' -AsSecureString
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
try {
    $connection['Host'] = 'localhost'
    $connection['Port'] = $dbPort
    $connection['Database'] = 'pei_identidad_db'
    $connection['Username'] = $dbUser
    $connection['Password'] = [System.Net.NetworkCredential]::new('', $securePassword).Password
}
finally { $securePassword.Dispose() }

$bytes = [byte[]]::new(48)
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$jwtKey = [Convert]::ToBase64String($bytes)
[Array]::Clear($bytes, 0, $bytes.Length)
```

Se generan 48 bytes aleatorios y se guarda su representación Base64 como texto. El código de JWT
usa los bytes UTF-8 de ese texto al firmar y validar; no lo decodifica. Supera el mínimo de 32 bytes
UTF-8 requerido. No imprimir `$jwtKey` ni reutilizar claves productivas. Cambiarla invalida los JWT
ya emitidos: generar una vez por entorno, no en cada arranque habitual.

### Opción A: User Secrets para la API

Cargar mediante JSON por stdin evita colocar los valores sensibles en argumentos del proceso:

```powershell
$settings = @{
    'ConnectionStrings:IdentidadDb' = $connection.ConnectionString
    'Jwt:SecretKey' = $jwtKey
    'Jwt:Issuer' = 'PEI.Identidad'
    'Jwt:Audience' = 'PEI'
    'Platform:Enabled' = 'false'
    'PlatformBootstrap:Enabled' = 'false'
    'DevelopmentTools:EnableTestTokens' = 'false'
}
$settings | ConvertTo-Json -Compress | dotnet user-secrets set --project $api
if ($LASTEXITCODE -ne 0) { throw 'No se pudieron cargar los secretos locales.' }
$settings.Clear()
# Evitar que una configuración anterior del proceso oculte estos dos secretos.
Remove-Item Env:Jwt__SecretKey, Env:ConnectionStrings__IdentidadDb -ErrorAction SilentlyContinue
```

Comprobar solo la presencia de las claves; no mostrar las líneas recibidas del comando:

```powershell
$secretLines = @(dotnet user-secrets list --project $api)
if ($LASTEXITCODE -ne 0) { throw 'No se pudo comprobar User Secrets.' }
foreach ($key in @('ConnectionStrings:IdentidadDb', 'Jwt:SecretKey')) {
    $present = @($secretLines | Where-Object { $_.StartsWith("$key = ", [StringComparison]::OrdinalIgnoreCase) }).Count -eq 1
    Write-Output "$key configurada: $present"
}
$secretLines = $null
```

### Opción B: variables de entorno del proceso

Alternativa sin persistir User Secrets. Configurar todos los valores necesarios, incluidos issuer y
audience para comandos ejecutados sin los perfiles de lanzamiento:

```powershell
$env:ConnectionStrings__IdentidadDb = $connection.ConnectionString
$env:Jwt__SecretKey = $jwtKey
$env:Jwt__Issuer = 'PEI.Identidad'
$env:Jwt__Audience = 'PEI'
```

Las opciones predeterminadas son AccessTokenMinutes=15 y ClockSkewSeconds=15. Los intervalos
admitidos son 1–60 minutos y 0–60 segundos, respectivamente. No cargar secretos mediante
`setx`, archivos versionados, valores literales en el historial ni parámetros `--connection`.
Las variables de entorno contienen texto plano y se heredan a los procesos hijos.

## 4. Migraciones: verificar primero, aplicar solo con autorización

Los dos contextos comparten base y esquema. La API no migra al iniciar. **Aunque Platform esté
deshabilitada funcionalmente, la preparación completa de persistencia incluye ambos contextos.**

La diferencia de configuración es importante:

- IdentidadDbContext se obtiene del host de la API: necesita la conexión y la configuración JWT
  válida; en Development puede cargar los User Secrets de la API.
- PlataformaDbContextFactory usa únicamente AddEnvironmentVariables: **no lee User Secrets,
  appsettings ni la conexión que se haya guardado solo en la API**. Requiere explícitamente
  ConnectionStrings__IdentidadDb. No requiere habilitar Platform ni ejecutar su bootstrap.

Para que ambos comandos apunten al mismo lugar, en la misma sesión anterior:

```powershell
$env:ConnectionStrings__IdentidadDb = $connection.ConnectionString
dotnet restore .\services\identidad\PEI.Identidad.sln
if ($LASTEXITCODE -ne 0) { throw 'Falló restore.' }
dotnet build .\services\identidad\PEI.Identidad.sln --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }

# Solo inventario del código; no consulta ni modifica una base.
dotnet ef migrations list --no-connect --project $infra --startup-project $api --context IdentidadDbContext
if ($LASTEXITCODE -ne 0) { throw 'Falló el inventario institucional.' }
dotnet ef migrations list --no-connect --project $infra --startup-project $api --context PlataformaDbContext
if ($LASTEXITCODE -ne 0) { throw 'Falló el inventario de plataforma.' }
```

Con --no-connect puede aparecer el aviso de que no se muestra el estado pendiente/aplicado.
Es esperado: ese inventario solo demuestra qué migraciones contiene el código, no cuáles están
aplicadas a la base. Tampoco interpretar un código de salida cero con avisos de conexión en
el inventario conectado como prueba de un historial verificado; revisar la salida y detenerse
si EF no pudo determinarlo.

Antes de aplicar, verificar la **conexión efectiva**, sin imprimir su contraseña, y consultar el
servidor desde el host con ese mismo destino. Este bloque solo ejecuta lecturas:

```powershell
$env:PGPASSWORD = [string]$connection['Password']
try {
    psql -X -v ON_ERROR_STOP=1 -h localhost -p $dbPort -U $dbUser -d pei_identidad_db -c 'BEGIN TRANSACTION READ ONLY; SELECT current_database(), current_user, current_schema(), current_setting(''search_path''), inet_server_addr(), inet_server_port(); SELECT schemaname, tablename FROM pg_tables WHERE schemaname = ''public'' ORDER BY tablename; COMMIT;'
    if ($LASTEXITCODE -ne 0) { throw 'No se verificó el destino; no aplicar migraciones.' }
}
finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }

# Consulta de historiales existentes; no modifica datos.
dotnet ef migrations list --project $infra --startup-project $api --context IdentidadDbContext
if ($LASTEXITCODE -ne 0) { throw 'No se verificó el historial institucional.' }
dotnet ef migrations list --project $infra --startup-project $api --context PlataformaDbContext
if ($LASTEXITCODE -ne 0) { throw 'No se verificó el historial de plataforma.' }
```

Confirmar pei_identidad_db y esquema public, puerto publicado y propietario del contenedor.
inet_server_port devuelve normalmente **5432 dentro de Docker**, no el puerto publicado del host.
Una respuesta correcta no autoriza a modificar una base compartida. Si hay tablas preexistentes
sin historial compatible, esquema distinto, migraciones inesperadas o datos ajenos, detenerse.
Revisar con el responsable, prever copia de seguridad y consultar el preflight de
[normalización de correos](H9-normalizacion-emails.md) antes de actualizar una base con usuarios.

Solo después de esa revisión y autorización manual, aplicar **primero Identidad y después Plataforma**:

```powershell
$confirmation = Read-Host 'Con destino revisado y autorizado, escribir APLICAR IDENTIDAD LOCAL'
if ($confirmation -cne 'APLICAR IDENTIDAD LOCAL') { throw 'Migraciones canceladas.' }
dotnet ef database update --project $infra --startup-project $api --context IdentidadDbContext
if ($LASTEXITCODE -ne 0) { throw 'Falló Identidad; no continuar con Plataforma.' }
dotnet ef database update --project $infra --startup-project $api --context PlataformaDbContext
if ($LASTEXITCODE -ne 0) { throw 'Falló Plataforma; revisar antes de continuar.' }
```

Identidad: InitialCreate → ProtectIdentityInvariants → NormalizeGlobalEmails, historial
`__EFMigrationsHistory`. Plataforma: InitialPlatformSchema → PlatformInvitations, historial
`__EFMigrationsHistoryPlataforma`. La primera migración de Plataforma exige funciones y triggers
institucionales. No usar EnsureCreated, downgrades, borrado de datos ni eliminación de triggers
para resolver diferencias. Esta guía no ejecuta migraciones automáticamente.

## 5. Arranque directo y Swagger

Con la misma sesión configurada, iniciar el perfil HTTPS:

```powershell
dotnet dev-certs https --check
# Solo si falta un certificado de desarrollo confiable, revisar y ejecutar manualmente:
# dotnet dev-certs https --trust
dotnet run --project $api --launch-profile https
```

Comprobar en la consola las URLs de escucha. El perfil https publica **https://localhost:7270**
y **http://localhost:5293**; el perfil http publica solo **http://localhost:5293**.
Swagger: https://localhost:7270/swagger y JSON https://localhost:7270/swagger/v1/swagger.json.
Swagger también está disponible por HTTP en Development. No desactivar validación TLS para
ocultar errores de certificado ni cambiar autenticación para facilitar el arranque.

En otra consola, sin secretos para esta comprobación pública de Swagger:

```powershell
$response = Invoke-WebRequest 'https://localhost:7270/swagger/v1/swagger.json'
if ($response.StatusCode -ne 200) { throw 'Swagger no respondió correctamente.' }
$schema = $response.Content | ConvertFrom-Json
Write-Output ('Login documentado: ' + ($null -ne $schema.paths.'/api/auth/login'))
```

Esto verifica arranque HTTP y generación OpenAPI, **no conectividad con PostgreSQL ni cuentas
válidas**. La comprobación de base corresponde al paso anterior. Una base recién migrada no
contiene usuarios: login puede responder 401 legítimamente. No habilitar registro público,
tokens de prueba ni bootstrap para cambiar ese resultado.

### Gateway: 5101 es otro escenario

El Gateway configura Identidad en http://localhost:5101, mientras los perfiles de la API usan
5293/7270. No se cambian esos archivos en ID-09. Para escuchar explícitamente en el puerto
esperado por Gateway, en lugar del arranque directo anterior:

```powershell
dotnet run --project $api --launch-profile http -- --urls http://localhost:5101
```

Usar directamente http://localhost:5101/swagger para comprobar ese proceso. Esto resuelve solo
el puerto: Gateway transforma sus rutas hacia /api/v1/..., mientras el controlador de sesión
expone /api/auth/.... La integración de rutas requiere coordinación aparte; no se afirma que
cambiar el puerto haga funcional el flujo completo del Gateway. Para este ticket, validar la API
directamente. El perfil solo HTTP puede advertir que no determina el puerto HTTPS de redirección;
para el recorrido habitual usar el perfil https con certificado confiable.

## 6. Plataforma y documentación especializada

Mantener Platform:Enabled=false, PlatformBootstrap:Enabled=false y
DevelopmentTools:EnableTestTokens=false en el recorrido básico. No necesitan claves globales.
Para probar aprovisionamiento e invitaciones, seguir [ID-06](ID06-ADMINISTRACION-GLOBAL.md):
Platform requiere clave, issuer y audience diferentes de los institucionales, URL de activación,
directorios privados absolutos para claves y correo y habilitación explícita. No copiar el JWT
institucional a Platform ni ejecutar bootstrap sin su procedimiento controlado.

Referencias: [seguridad ID-02/ID-05](ID-02-ID-05-seguridad.md),
[normalización](H9-normalizacion-emails.md), [JWT e integración](INTEGRACION-ID01-ID03-ID05.md)
y [sesiones ID-04/ID-06](INTEGRACION-ID04-ID06.md). Los informes históricos describen sus
validaciones originales; no certifican el estado de una base local actual.

## 7. Errores frecuentes y pruebas

| Síntoma | Verificación o solución |
|---|---|
| Falta IdentidadDb | Configurar ConnectionStrings:IdentidadDb en User Secrets o su variable con doble guion bajo. |
| JWT inválido al arrancar | Revisar presencia de SecretKey, issuer/audience, longitud UTF-8 y duración; no imprimir la clave. |
| EF Plataforma ignora User Secrets | Definir ConnectionStrings__IdentidadDb en el proceso que ejecuta dotnet ef. |
| Conexión rechazada | Revisar Docker y puerto real publicado; 5441 es el Compose versionado, 5541 no se debe asumir. |
| Contraseña rechazada tras editar .env | La credencial del volumen existente no se actualiza cambiando el archivo. Consultar al responsable. |
| Falta pei_normalize_email o tablas/columnas | Revisar contexto, historial, esquema y orden de migraciones; no usar EnsureCreated. |
| Colisión de correo durante migración | Detenerse y revisar preflight H9; no eliminar ni renombrar cuentas automáticamente. |
| Swagger 404 | Verificar entorno Development, perfil y puerto de escucha; no usar /swagger como prueba de base. |
| HTTPS no confiable | Revisar certificado local con dev-certs; no deshabilitar su validación. |
| 401/403 | Distinguir falta de cuenta/token válido de falta de rol; conservar separación institucional/global. |
| Gateway 502/404 | Revisar puerto 5101 y diferencia de rutas; usar acceso directo para validar Identidad. |

Pruebas sin PostgreSQL: ejecutar el proyecto unitario/HTTP. Sus dobles no necesitan una base real:

```powershell
dotnet test .\services\identidad\tests\PEI.Identidad.UnitTests --no-restore
```

Las integraciones exigen PEI_IDENTIDAD_TEST_CONNECTION externo hacia **pei_identidad_tests**
en localhost/127.0.0.1/::1:**5541**, con esquemas efímeros propios. No apuntarlas a la base de
desarrollo. Sin esa variable se reportan omitidas, no aprobadas. Ver el procedimiento y permisos
en ID-02/ID-05; ID-09 no crea una base o contenedor de pruebas adicional.

## 8. Cierre de la sesión

Detener la API con Ctrl+C. Retirar los secretos y referencias en memoria del proceso local
cuando ya no se ejecuten API ni herramientas. User Secrets permanece almacenado para el próximo
arranque y no se elimina automáticamente; no usar clear si contiene configuración de otros trabajos.

Los secretos permanecen en memoria durante la sesión. Limpiar variables o asignarles null no
garantiza eliminar todas sus copias de memoria, incluidas las que puedan conservar procesos hijos;
estos comandos no equivalen a un borrado seguro.

```powershell
Remove-Item Env:ConnectionStrings__IdentidadDb, Env:Jwt__SecretKey -ErrorAction SilentlyContinue
if ($null -ne $connection) { $connection.Clear() }
$jwtKey = $null
$secretLines = $null
```

No publicar secretos, .env ni salidas completas de configuración. No hacer down -v ni borrar
volúmenes como parte del cierre. La guía documenta operaciones manuales; aplicar cambios a una
base existente siempre requiere la verificación y autorización descritas arriba.

## Validación de esta guía — 2026-10-22

Revisión realizada sobre main f1ef6b7, en el worktree de ID-09. Se modificaron únicamente
esta guía y la documentación de Identidad del README principal.

- Restore y compilación de PEI.sln: correctos, 0 errores y 0 advertencias de compilación.
- Análisis sintáctico de los 17 bloques PowerShell: correcto, sin ejecutar las cargas de secretos,
  operaciones Docker ni comandos de aplicación de migraciones documentados.
- Inventario EF de ambos contextos con --no-connect: reconoció las tres migraciones de Identidad
  y las dos de Plataforma. Configuración temporal con clave aleatoria y conexión ficticia a
  127.0.0.1:1/id09_no_connect, sin credenciales de una base real.
- Arranque temporal en Development con Platform deshabilitada: Swagger UI y JSON respondieron
  HTTP 200, con login y esquema Bearer documentados. Se usó un puerto efímero y se detuvo el
  proceso al terminar. Esto no valida certificados HTTPS ni conectividad PostgreSQL.
- Pruebas de Identidad: 112 unitarias/HTTP aprobadas, 0 fallidas; 48 integraciones PostgreSQL
  omitidas al retirar PEI_IDENTIDAD_TEST_CONNECTION del proceso. No equivalen a aprobadas.

No se aplicaron migraciones, cargaron semillas ni modificaron bases existentes. No se inició ni
recreó ningún contenedor. El contenedor ya presente publicaba 5541, distinto del 5441 versionado:
no se lo alteró para validar la guía. La comprobación real del destino y la aplicación autorizada
de migraciones siguen siendo pasos manuales pendientes para el operador de cada entorno.
