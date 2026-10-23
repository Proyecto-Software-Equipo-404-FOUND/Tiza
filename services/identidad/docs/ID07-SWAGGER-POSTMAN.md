# ID-07 — Swagger/OpenAPI y Postman de Identidad

## Alcance

Documentación de las once operaciones actuales de autenticación institucional, registro,
comprobación de roles y Plataforma. No se cambian rutas, métodos, claims, roles,
serialización, controladores, persistencia o políticas productivas. WeatherForecast permanece
intacto y no forma parte de la colección. No se publica un contrato definitivo en contracts/openapi.

Los tipos terminados en ResponseDocumentation solo describen los objetos anónimos actuales;
no reemplazan los cuerpos que construyen las acciones.

## Swagger en Development

Configurar externamente la conexión y JWT conforme al procedimiento local autorizado.
No copiar secretos a appsettings ni a este documento. Desde la raíz:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project services/identidad/src/PEI.Identidad.Api --launch-profile http
```

Abrir http://localhost:5293/swagger y el documento http://localhost:5293/swagger/v1/swagger.json.
El perfil HTTPS usa https://localhost:7270. Swagger solo se publica en Development.
El arranque HTTP no es el procedimiento de bootstrap ni ejecuta semillas o migraciones.

En Authorize, Bearer corresponde al JWT institucional con institucion_id; PlatformBearer
corresponde exclusivamente al JWT global, sin institución. No son intercambiables.
Login, refresh, logout, login global y activación no requieren access token.
Refresh y logout reciben refreshToken en JSON; un JWT no sustituye un refresh token.

## Operaciones y respuestas

| Método y ruta | Éxito | Errores documentados | Autorización |
|---|---|---|---|
| POST /api/auth/login | 200 SessionResponse | 400,401,415,429,500 | Anónimo |
| POST /api/auth/refresh | 200 SessionResponse | 400,401,415,429,500 | Anónimo |
| POST /api/auth/logout | 204 sin cuerpo | 400,415,429,500 | Anónimo |
| POST /api/auth/register | 201 id y message | 400,401,403,409,415,500 | Bearer: Administrador |
| GET /api/auth/administrador | 200 mensaje | 401,403,500 | Bearer: Administrador |
| GET /api/auth/docente | 200 mensaje | 401,403,500 | Bearer: Docente |
| GET /api/auth/alumno | 200 mensaje | 401,403,500 | Bearer: Alumno |
| POST /api/plataforma/auth/login | 200 accessToken y tokenType | 400,401,404,415,429,500 | Anónimo |
| POST /api/plataforma/instituciones | 201 institucionId y administradorId | 400,401,403,404,409,415,500 | PlatformBearer: AdministradorGlobal |
| GET /api/plataforma/instituciones | 200 lista InstitucionResumen | 400,401,403,404,500 | PlatformBearer: AdministradorGlobal |
| POST /api/plataforma/invitaciones/activar | 204 sin cuerpo | 400,401,404,415,429,500 | Anónimo |

Los errores se describen con ProblemDetails. Los 400 admiten también ValidationProblemDetails
con errors. Los challenges 401/403 de PlatformBearer conservan application/json; los errores
manejados por excepciones y los institucionales usan application/problem+json. Se documentan
ambos contenidos para esos códigos globales, sin modificar el comportamiento existente.
El 500 representa fallos técnicos; la salida diagnóstica depende del entorno y negociación.

Plataforma requiere Platform:Enabled y configuración segura propia. Deshabilitada, las acciones
retornan 404 si llegan a ejecutarse; autenticación, autorización o validación pueden rechazar antes.
No habilitar Plataforma solo para explorar Swagger: el documento ya describe sus operaciones.
El listado usa pagina de 1 a 1000000, 50 elementos por página como máximo, ordenados por Id.
El registro solo admite Docente o Alumno y toma la institución del administrador autenticado;
no envía institucionId. El administrador institucional inicial se incorpora por invitación.

## Importación y variables de Postman

Importar desde tests/postman:

- identidad.postman_collection.json
- identidad.local.postman_environment.json

Seleccionar el entorno importado. baseUrl apunta directamente a http://localhost:5293,
sin barra final. Puede utilizarse el perfil HTTPS local con su certificado confiable.
No desactivar globalmente la verificación TLS. Ninguna petición utiliza Gateway.

El ejemplo contiene correos sintéticos .test, identificadores vacíos y ningún secreto.
Introducir contraseñas, invitaciones y tokens únicamente en valores locales no compartidos,
o mediante el mecanismo de secretos de Postman disponible para el operador. Marcar un valor
como secreto solo oculta su presentación: no garantiza que una exportación lo excluya.
Revisar y vaciar valores sensibles antes de exportar; no sincronizarlos, versionarlos ni imprimirlos.
Los secretos pueden permanecer en memoria o en el almacenamiento del cliente; limpiar variables
no garantiza eliminar todas sus copias.

Variables separadas:

- institutionalToken y refreshToken: actualizados por login/refresh institucional.
- institutionalAdminToken, institutionalTeacherToken e institutionalStudentToken: tokens
  vigentes de cuentas de prueba con esos roles. Configurarlos explícitamente tras autenticar
  cada cuenta; los scripts no infieren privilegios ni copian un token a otro rol.
- platformToken: actualizado solo por login global; no hay refresh global en esta colección.
- institutionalEmail/Password y platformEmail/Password: credenciales externas.
- newUserEmail/Password/Name/Surname/Role: nuevo usuario sintético; rol Alumno o Docente.
- institutionName/Code, invitedAdminEmail/Password, invitationToken: alta e invitación sintéticas.
- createdUserId y createdInstitutionId: resultados de altas; page: página a consultar.

No se presupone que ID-08 esté cargado ni que esos correos existan. El operador debe disponer
de cuentas sintéticas autorizadas. No agregar usuarios automáticamente para preparar Postman.

## Lectura frente a operaciones con efectos

Las carpetas 3 y 5 y la subcarpeta Solo lectura de Plataforma ejecutan GET. Los casos negativos
requieren tokens válidos de los esquemas/roles indicados, salvo el caso de JWT deliberadamente
inválido. Con Plataforma deshabilitada no ejecutar los casos que requieren su esquema activo.
Una variable Bearer vacía hace fallar la preparación para evitar falsos positivos por anonimato.

Login crea sesión; refresh consume y reemplaza el token; logout revoca. Registro, alta y activación
modifican datos. Login global emite credenciales. Estas peticiones requieren allowMutations=true;
por defecto están omitidas mediante pm.execution.skipRequest(), disponible en versiones actuales
de Postman. Verificar soporte antes de usar runners externos o antiguos.

No ejecutar toda la colección sobre una base persistente. Para flujos de escritura usar un entorno
desechable autorizado, suministrar las contraseñas externas y ejecutar peticiones individualmente.
Login y refresh guardan el nuevo refreshToken; ejecutar logout al finalizar la sesión deseada.
Los correos/códigos no se aleatorizan: repetir un alta puede devolver 409; no es una prueba de
idempotencia ni una orden para borrar o modificar registros existentes. La invitación se obtiene
por el canal autorizado, nunca desde una tabla ni un endpoint de bootstrap inventado.
Los scripts comprueban códigos y estructuras básicas; no registran valores de tokens.

## Validación automatizada sin PostgreSQL persistente

```powershell
dotnet build services/identidad/PEI.Identidad.sln
dotnet test services/identidad/tests/PEI.Identidad.UnitTests/PEI.Identidad.UnitTests.csproj --no-build --no-restore
git diff --check
```

Las unitarias HTTP usan TestServer y dobles de persistencia con destino not_used, sin abrir
PostgreSQL. Cubren códigos, cuerpos, seguridad OpenAPI, validación, 415 y separación de JWT.
No ejecutar aquí el proyecto IntegrationTests: aplica migraciones y escribe en esquemas de prueba.
No se ejecutan Newman ni la colección contra una API persistente como parte de ID-07.