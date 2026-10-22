# Auditoría e integración local de ID-04 e ID-06

Fecha: 21 de octubre de 2026.

## 1. Procedencia y alcance

- Worktree independiente: `C:/Users/Usuario/Documents/Codex/2026-10-19/auditor-a-t-cnica-de-pei/work/id04-id06-integration`.
- Rama temporal: `integration/ID-04-ID-06`, creada desde ID-06 `e62141e6eb3e6aeb3300c91ca7e3b4f7cc10d150`.
- ID-04 inspeccionada: `origin/feature/ID-04-cierre-renovacion-roles`, `5c0f77476b395023eae7ad0f19d2943391a94394`.
- Ancestro común verificado: `5587c48832d461a65219258abc2050ec4f1e7a48`.
- Referencia origin/main consultada: `e92b99492d8881f7472e90750d0f10863889bc8e`.

Se revisaron los cambios completos de Identidad desde el ancestro y los componentes de persistencia,
seguridad, autenticación, plataforma y pruebas involucrados. `git merge-tree --write-tree` reprodujo
los once conflictos indicados y confirmó que main e ID-06 se combinan sin conflictos textuales.
Eso no acredita por sí solo compatibilidad funcional.

La integración se realizó por adaptación selectiva del código de ID-04 sobre ID-06, sin iniciar
un merge pendiente ni crear un commit. No se incorporaron indiscriminadamente repositorios o
configuraciones anteriores incompatibles con ID-05. El índice está vacío y todos los cambios de
integración están en el working tree, incluidos archivos nuevos sin seguimiento.

El checkout original conserva sus auxiliares `migracion-plataforma-revision.sql` y
`services/identidad/.github/`. No se copiaron al worktree ni se modificaron. No se alteraron main,
las ramas originales, sus PR ni referencias remotas. No hubo fetch, push, rebase ni commits.

## 2. Hallazgos de compatibilidad y seguridad

| Severidad | Evidencia en ID-04, commit 5c0f774 | Consecuencia de una integración directa | Resolución |
|---|---|---|---|
| Alta | `RefreshTokenRepository.GetByTokenAsync`, líneas 20–25, y `UsuarioRepository.GetByEmailAsync`, líneas 17–22 | Consultas ordinarias antes de tener JWT quedan cerradas por los filtros de ID-05. Login/refresh fallarían sin tenant. | Usar IAuthenticationDataAccess y IAuthenticationTokenWriter existentes; consultas privilegiadas acotadas, sin repositorio general sin filtros. |
| Crítica | `AuthController.Register`, líneas 40–46; `RegisterCommand.InstitucionId`; catálogo de RegisterHandler con Administrador | Registro anónimo permite elegir institución y crear administradores, contrario a las decisiones aprobadas. | Conservar InstitutionAdmin, tenant derivado del JWT y solo Docente/Alumno de ID-06. El primer administrador sigue exclusivamente el flujo global con invitación. |
| Alta | `JwtTokenService`, líneas 35–40, emite ClaimTypes.Role e institucionId | JwtContract estricto de ID-06 rechaza esos aliases; relajar el validador introduciría dos contratos ambiguos. | Conservar role/sub/institucion_id canónicos, MapInboundClaims=false y validación actual de actividad/rol. |
| Alta | `RefreshHandler`, líneas 57–72, lectura seguida de revocación y alta mediante entidades rastreadas | El flujo no utiliza el arbitraje condicional ya disponible en ID-05; su combinación con xmin tampoco resuelve respuestas de concurrencia como autenticación fallida. | Rotación por CTE atómico existente: un ganador, actualización/alta indivisibles y rollback si falla el reemplazo. |
| Media | LoginHandler y RefreshHandler fijan accessTokenExpiresAt a 60 minutos | El JWT integrado usa duración configurable de 1–60 minutos; la respuesta podría mentir sobre su vigencia. | Nuevo IAccessTokenIssuer devuelve token y vencimiento de la misma emisión. Se conserva ITokenService para sus consumidores anteriores. |
| Media | Swagger de Program aplica Bearer globalmente | Documentación exigiría token en login y podría confundir credenciales institucionales y globales. | Dos definiciones y requisitos por operación, sin requisito global indiscriminado. |
| Media | IPasswordHasher de ID-04 carece de VerifyDummy; BCrypt anterior no conserva todas las validaciones de ID-06 | Pérdida de defensa ante enumeración temporal y de límite de bytes BCrypt al resolver por una sola versión. | Conservar interfaz y hasher endurecidos, adaptar pruebas y casos de uso. |
| Media | Paquete JwtBearer 8.0.0 agregado por ID-04 | Una combinación textual no debe rebajar la versión integrada 8.0.32 ni desalinear IdentityModel. | Mantener referencias de ID-06; no se necesitan paquetes nuevos. |

Los problemas se resolvieron dentro del alcance y con decisiones funcionales ya aprobadas. No se
habilitó registro público, suplantación de institución ni refresh global como supuesto de integración.

## 3. Resolución de los once conflictos

Las rutas de la tabla son relativas a `services/identidad`.

| Archivo | Conservado de ID-04 | Conservado de ID-06 | Adaptación final |
|---|---|---|---|
| src/PEI.Identidad.Api/Controllers/AuthController.cs | Login con refresh y vencimientos; refresh, logout y tres endpoints de comprobación de roles | Registro administrativo protegido, validación HTTP y errores centralizados | Controlador delgado; sesión emitida por servicio de aplicación; sin Location a un recurso inexistente ni registro público. |
| src/PEI.Identidad.Api/Program.cs | Registros DI para refresh/logout y descripción Swagger de bearer | Ambos esquemas, middleware, validadores, rate limiting, bootstrap y worker | Agregar servicios institucionales; describir permisos por operación. |
| src/PEI.Identidad.Application/Interfaces/Security/IPasswordHasher.cs | Hash/Verify | VerifyDummy | Archivo ID-06 sin cambios; no duplicar contratos. |
| src/PEI.Identidad.Application/Interfaces/Service/ITokenService.cs | GenerateToken(Usuario), coincidente en ambas ramas | Contrato existente para todos los consumidores | Sin cambios; metadata adicional mediante IAccessTokenIssuer, implementado por el mismo emisor. |
| src/PEI.Identidad.Application/UseCases/Auth/Login/LoginCommand.cs | Email y Password | Validación de longitudes/formato/bytes | Conservar DTO de ID-06. |
| src/PEI.Identidad.Application/UseCases/Auth/Login/LoginHandler.cs | Autenticación previa a emisión de sesión | Acceso acotado, verificación ficticia, roles canónicos, independencia de navegaciones cargadas | Extraer AuthenticateAsync reutilizable; mantener HandleAsync para compatibilidad. InstitutionalSessionService emite/persiste la sesión completa. |
| src/PEI.Identidad.Application/UseCases/Auth/Register/RegisterCommand.cs | Datos de usuario | Sin tenant enviado por cliente; solo roles asignables | Sin cambios: no recuperar InstitucionId ni asignación pública de Administrador. |
| src/PEI.Identidad.Application/UseCases/Auth/Register/RegisterHandler.cs | Validaciones de registro y hashing ya representadas | Locks del actor, tenant, unicidad histórica y límites de roles | Sin cambios; conflictos de RegistroEmail continúan como 409 genérico. |
| src/PEI.Identidad.Infrastructure/Security/BCryptPasswordHasher.cs | BCrypt.Net-Next | Costo 12, límite de 72 bytes y hashes inválidos controlados | Sin cambios. |
| src/PEI.Identidad.Infrastructure/Security/JwtTokenService.cs | Firma HS256 y emisión institucional | Claims canónicos, configuración validada, jti/iat | Issue entrega el mismo JWT y su expiración exacta; GenerateToken delega. |
| tests/PEI.Identidad.UnitTests/UnitTest1.cs | Casos funcionales de login, registro, refresh y logout | AuthHandlerTests y suite de seguridad existentes | No restaurar clase duplicada AuthHandlerTests ni pruebas que legitimen registro público; cubrir sus casos en AuthHandlerTests e InstitutionalSessionTests adaptados. |

`UnitTest1.cs` de ID-04 no era una plantilla vacía: contenía quince pruebas. Ocho casos de login/registro
ya están representados en AuthHandlerTests de ID-06. Los siete de refresh/logout se conservan como
comportamientos en InstitutionalSessionTests: rotación, token inexistente/vencido/revocado, usuario
inactivo y logout existente/inexistente. Se amplían con institución inactiva, rol inválido, contratos
HTTP, esquema global y Swagger. No se trasladó la dependencia de sus dobles sobre repositorios inseguros.

## 4. Otros archivos y decisiones de arquitectura

### Código de ID-04 trasladado

- LoginResult: mismo record con AccessToken, RefreshToken y ambos vencimientos.
- IRefreshTokenService/RefreshTokenService: RandomNumberGenerator de 64 bytes y Base64.
- IRefreshTokenHasher/RefreshTokenHasher: SHA-256 hexadecimal; únicamente hashes en PostgreSQL.
- RefreshCommand y LogoutCommand: mismo campo RefreshToken, con máximo de 256 caracteres.
- RefreshHandler y LogoutHandler: mismos casos de uso, reimplementados sobre los puertos seguros.

IRefreshTokenHasher se ubicó en Interfaces/Security, coherente con su namespace original, en lugar
de mantener el archivo en Interfaces/Service. No se crearon dos contratos equivalentes.

No se trasladaron IUsuarioRepository/UsuarioRepository ni IRefreshTokenRepository/RefreshTokenRepository:
sus responsabilidades están cubiertas por IRegistrationDataAccess, IAuthenticationDataAccess y
IAuthenticationTokenWriter. Portarlos introduciría una segunda ruta de autenticación incompatible
con filtros/normalización y una segunda implementación de rotación. Esta omisión es deliberada y
no elimina endpoints, escenarios de sesión ni persistencia de ID-04.

### Sesiones institucionales

InstitutionalSessionService coordina login y refresh. No contiene SQL ni referencias a infraestructura.
El JWT se firma mediante la abstracción IAccessTokenIssuer. Su vencimiento usa precisión de segundos,
igual al claim exp. La vigencia de refresh conserva los siete días de ID-04.

Login entrega la sesión únicamente después de persistir el hash para un usuario/institución activos.
Refresh consulta una proyección acotada de identidad, rechaza rol global/tenant vacío y usa la rotación
condicional existente. No usa CuentaPlataforma ni PlataformaTokenService. No devuelve un resultado si
pierde la carrera de rotación. Las comprobaciones de estado y los locks de ID-05 permanecen intactos.

Logout conserva la semántica de ID-04: endpoint anónimo basado en la posesión del refresh secreto,
idempotente y sin requerir un JWT aún vigente. Puede revocar un token expirado o de una institución
inactiva. No exige ITenantContext; tampoco devuelve datos del propietario. Login, refresh y logout
usan limitación de frecuencia. Las respuestas con tokens tienen Cache-Control: no-store y Pragma: no-cache.

El DTO de respuesta de login conserva accessToken/tokenType de ID-06 y agrega los campos de ID-04:
refreshToken, accessTokenExpiresAt y refreshTokenExpiresAt. Los clientes que usen deserialización
estricta deben admitir esos campos adicionales. El login global sigue entregando solamente access token.

### Persistencia y dependencias

No hay migraciones nuevas ni cambios en entidades, configuraciones, contextos, snapshots, triggers,
normalización, RegistroEmail, aislamiento, outbox o bootstrap. La revisión final autorizada corrige
el escritor de refresh: bloqueos explícitos antes de validar vencimiento y excepción tipada al crear sesión.
Se conservan JwtBearer/Mvc.Testing 8.0.32, IdentityModel 8.23.0, BCrypt 4.2.0 y EF/Npgsql 8.0.4.
La consulta de paquetes vulnerables no reportó vulnerabilidades en las fuentes configuradas; no es
una garantía de ausencia de vulnerabilidades.

## 5. Validación inicial de integración (antes de H1–H4)

| Verificación | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Identidad, unitarias/HTTP | 107 | 0 | 0 |
| Identidad, integración PostgreSQL | 43 | 0 | 0 |
| Otros proyectos de PEI.sln | 39 | 0 | 0 |
| Total | 189 | 0 | 0 |

`dotnet build services/identidad/PEI.Identidad.sln` y `dotnet build PEI.sln`: correctos, cero errores
y cero advertencias. `dotnet test PEI.sln --no-build --no-restore`: resultado de la tabla.
La ejecución inicial anterior a los últimos casos nuevos también pasó: 106 unitarias y 38 PostgreSQL.
Se conservaron las 92+38 de ID-06 y se añadieron 15 unitarias/HTTP y cinco de integración.

PEI_IDENTIDAD_TEST_CONNECTION no estaba configurada al comenzar. Se verificó el contenedor local
postgres:16-alpine en localhost:5541 y current_database() = pei_identidad_tests. La conexión se
inyectó solo en el proceso de pruebas, sin imprimir credenciales ni guardarlas en el repositorio.
El fixture validó el destino, creó esquemas aleatorios, aplicó allí las migraciones existentes y los
eliminó al finalizar. No hubo migraciones ni escrituras contra desarrollo/producción.

Cobertura agregada/adaptada:

- Login institucional devuelve tokens, expiraciones reales y almacenamiento por hash.
- Refresh válido rota; anterior reutilizado, expirado, inexistente o revocado no renueva.
- Concurrencia HTTP con PostgreSQL: un 200 y un 401; un único reemplazo persistido.
- Logout idempotente, sin tenant/JWT y también ante institución/usuario inactivos.
- Cambios de rol invalidan el JWT anterior y la renovación toma el rol actual.
- Token global rechazado por endpoints institucionales; JWT global no sirve como refresh.
- Refresh institucional jamás genera un token de plataforma; acceso global sigue rechazado.
- Flujo global completo: creación → outbox → invitación/activación → login institucional → refresh → logout.
- Registro institucional no ocupa un correo histórico reservado por CuentaPlataforma.
- Roles institucionales exactos y Swagger con el esquema correcto por operación.
- Regresión de ID-05 e ID-06: filtros, triggers, concurrencia, reservas, bootstrap, invitaciones y outbox.

No se omitieron pruebas automatizadas. No se ensayó un proveedor real de correo ni un despliegue
productivo; se conserva el proveedor de desarrollo y sus límites ya documentados en ID06.

## 6. Inventario de cambios respecto de e62141e

Rutas relativas a services/identidad.

Modificados:

- src/PEI.Identidad.Api/Controllers/AuthController.cs
- src/PEI.Identidad.Api/Program.cs
- src/PEI.Identidad.Application/Abstractions/IAuthenticationTokenWriter.cs
- src/PEI.Identidad.Application/UseCases/Auth/Login/LoginHandler.cs
- src/PEI.Identidad.Infrastructure/Authentication/AuthenticationTokenWriter.cs
- src/PEI.Identidad.Infrastructure/Security/JwtTokenService.cs
- tests/PEI.Identidad.IntegrationTests/PlataformaHttpFlowTests.cs
- tests/PEI.Identidad.IntegrationTests/TenantIsolationTests.cs
- tests/PEI.Identidad.UnitTests/AuthenticationHttpTests.cs
- tests/PEI.Identidad.UnitTests/PlataformaHttpTests.cs

Nuevos, todavía sin seguimiento:

- docs/INTEGRACION-ID04-ID06.md
- src/PEI.Identidad.Api/Contracts/SessionResponse.cs
- src/PEI.Identidad.Api/Security/IdentitySecurityOperationFilter.cs
- src/PEI.Identidad.Application/Exceptions/AuthenticationIdentityUnavailableException.cs
- src/PEI.Identidad.Application/Interfaces/Security/IRefreshTokenHasher.cs
- src/PEI.Identidad.Application/Interfaces/Service/IAccessTokenIssuer.cs
- src/PEI.Identidad.Application/Interfaces/Service/IRefreshTokenService.cs
- src/PEI.Identidad.Application/UseCases/Auth/Login/LoginResult.cs
- src/PEI.Identidad.Application/UseCases/Auth/Refresh/InstitutionalSessionService.cs
- src/PEI.Identidad.Application/UseCases/Auth/Refresh/RefreshCommand.cs
- src/PEI.Identidad.Application/UseCases/Auth/Refresh/RefreshHandler.cs
- src/PEI.Identidad.Application/UseCases/Auth/Refresh/LogoutCommand.cs
- src/PEI.Identidad.Application/UseCases/Auth/Refresh/LogoutHandler.cs
- src/PEI.Identidad.Infrastructure/Security/RefreshTokenHasher.cs
- src/PEI.Identidad.Infrastructure/Security/RefreshTokenService.cs
- tests/PEI.Identidad.IntegrationTests/ControlledSessionConcurrencyTests.cs
- tests/PEI.Identidad.IntegrationTests/InstitutionalSessionHttpTests.cs
- tests/PEI.Identidad.UnitTests/InstitutionalSessionTests.cs

Los dobles de prueba HTTP implementan los puertos de sesión para no conectar accidentalmente a
PostgreSQL en pruebas unitarias. Las propiedades de concurrencia se verifican además contra PostgreSQL.
No se modificaron otros microservicios, archivos auxiliares, .github ni infra/docker-compose.yml.

## 7. Riesgos pendientes y compatibilidad de despliegue

- Logout revoca el refresh presentado; no agrega lista de revocación inmediata de access JWT ni
  revocación de toda la familia de descendientes. Es la semántica existente de ID-04, conservada.
  Un access token válido puede vivir hasta exp, salvo que cambien actividad/rol y el validador lo rechace.
- Si refresh consume un token y se pierde la respuesta, reusar el anterior falla: puede requerir login.
  No se agregó una ventana de reutilización permisiva ni un protocolo nuevo de idempotencia.
- Un cambio de rol concurrente con la emisión puede producir un JWT ya obsoleto; la validación contra
  el rol actual lo rechaza. No habilita privilegios anteriores como fallback.
- No hay refresh global. Su diseño requeriría contratos y propietarios separados; no reutilizar IDs
  de CuentaPlataforma en RefreshToken.UsuarioId.
- Los JWT legacy de ID-04 con institucionId/ClaimTypes.Role no son admitidos. Requieren iniciar sesión
  con la API integrada. No relajar JwtContract para conservar aliases ambiguos.
- El registro público/selección de institución de ID-04 no se mantiene porque contradice las decisiones
  aprobadas. Los clientes deben usar el registro institucional autenticado o el aprovisionamiento global.
- Los requisitos productivos de ID-06 continúan: proveedor real de email, claves protegidas/persistidas,
  cliente de activación, recuperación operativa y política de MFA. No se validó producción por inferencia.
- Las referencias remotas son las disponibles localmente; no se actualizó ni cambió ningún PR. Si
  avanzan las ramas, repetir el análisis de diferencias antes de aprobar.

## 8. Recomendación de integración de PR

1. Revisar ID-06 y su base de seguridad primero; integrarlo a main mediante el proceso del equipo.
2. Adaptar después el PR de ID-04 contra esa base usando los cambios revisados de este worktree,
   conservando la trazabilidad de su autor. No aprobar su versión original sin esta adaptación.
3. Ejecutar nuevamente la suite completa sobre el estado final de main y ambos cambios antes de aprobar.

Esta rama temporal contiene una integración funcional por adaptación, no un merge commit que convierta
automáticamente 5c0f774 en ancestro. El responsable decidirá cómo trasladarla al PR autorizado tras revisar
el diff y los archivos nuevos. No se hicieron modificaciones de PR, commits ni push.

Veredicto: apto para revisión técnica de la integración local. Worktree conservado, rama temporal
en e62141e con cambios sin confirmar e índice vacío. No equivale a aprobación automática de los PR
originales ni a autorización para desplegar o aplicar migraciones en una base existente.

## 9. Correcciones de la auditoría final H1–H4

Las cuatro correcciones fueron autorizadas expresamente, conservando la rama temporal y el worktree.

- H1: AuthenticationTokenWriter conserva una sola sentencia atómica. Un CTE materializado bloquea
  usuario/institución y otro bloquea el refresh token. La condición de vencimiento se evalúa sobre
  la fila del segundo CTE con clock_timestamp(), después de ambos bloqueos. No usa una hora del
  proceso capturada antes de esperar. El INSERT sigue dependiendo del UPDATE condicional y un
  error de reemplazo revierte el consumo, cubierto también por la regresión existente.
- H2: AuthenticationIdentityUnavailableException distingue el resultado de identidad no disponible
  de errores técnicos. Solo ese resultado se convierte en credenciales inválidas. InvalidOperationException
  y otros fallos técnicos se propagan al manejo de errores del servidor. Se documenta el contrato del
  puerto y se adapta la prueba existente que verificaba la excepción de identidad inactiva.
- H3: SessionResponse conserva exactamente los cinco campos JSON de sesión. Login y refresh
  documentan 200/400/401/429/500; logout documenta 204/400/429/500, con 204 sin cuerpo. Los errores
  usan application/problem+json. No se agregan requisitos de autenticación a operaciones anónimas.
- H4: ControlledSessionConcurrencyTests usa una transacción bloqueante y conexiones independientes.
  pg_stat_activity y pg_blocking_pids confirman que las solicitudes realmente esperan locks. No basta
  con iniciar dos tareas. Se verifican expiración bajo lock de propietario y de token, dos refresh
  contendiendo, y ambos órdenes de la carrera refresh/logout.

Las pruebas de expiración comprueban primero que el token sigue vigente mientras la solicitud está
bloqueada y esperan el reloj real de PostgreSQL antes de liberar el lock. Los polls tienen timeout
y no se modifica el vencimiento para eludir triggers. Los comentarios SQL constantes identifican
las operaciones sin contener secretos ni identificadores de usuarios.

La carrera conserva la semántica aprobada: si logout consume primero el token, refresh devuelve 401;
si refresh gana primero, logout del token anterior no revoca automáticamente a su descendiente.
No se cambian roles, políticas, esquemas JWT, vigencias aprobadas ni el alcance de logout.

Archivos modificados/agregados en esta corrección, relativos a services/identidad:

- src/PEI.Identidad.Infrastructure/Authentication/AuthenticationTokenWriter.cs
- src/PEI.Identidad.Application/Abstractions/IAuthenticationTokenWriter.cs
- src/PEI.Identidad.Application/Exceptions/AuthenticationIdentityUnavailableException.cs (nuevo)
- src/PEI.Identidad.Application/UseCases/Auth/Refresh/InstitutionalSessionService.cs
- src/PEI.Identidad.Api/Contracts/SessionResponse.cs (nuevo)
- src/PEI.Identidad.Api/Controllers/AuthController.cs
- src/PEI.Identidad.Api/Security/IdentitySecurityOperationFilter.cs
- tests/PEI.Identidad.UnitTests/AuthenticationHttpTests.cs
- tests/PEI.Identidad.UnitTests/InstitutionalSessionTests.cs
- tests/PEI.Identidad.IntegrationTests/TenantIsolationTests.cs
- tests/PEI.Identidad.IntegrationTests/ControlledSessionConcurrencyTests.cs (nuevo)
- docs/INTEGRACION-ID04-ID06.md

### Resultado final después de H1–H4

| Verificación | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Identidad: unitarias/HTTP | 112 | 0 | 0 |
| Identidad: PostgreSQL | 48 | 0 | 0 |
| Otros proyectos de PEI.sln | 39 | 0 | 0 |
| Total final | 199 | 0 | 0 |

`dotnet build PEI.sln --no-restore`: cero errores y cero advertencias.
`dotnet test PEI.sln --no-build --no-restore`: resultado de la tabla. Las diez pruebas nuevas
(cinco unitarias/HTTP y cinco PostgreSQL) también pasaron primero en una ejecución focalizada.
La suite completa volvió a comprobar el rollback ante un reemplazo inválido y las regresiones ID-05/ID-06.

Solo se usaron los esquemas efímeros del fixture dentro de pei_identidad_tests local:5541. No se
aplicaron migraciones a bases existentes de desarrollo/producción ni se agregaron migraciones.
No se cambió ninguna decisión funcional: las correcciones eliminan los bordes señalados en la auditoría.

Sin cambios en staging, sin commits, push, merges ni modificaciones a ramas originales/PR.
Worktree conservado para revisión. Los riesgos operativos y la semántica de sesión descritos en
la sección 7 continúan vigentes; no quedan pruebas automatizadas omitidas ni fallidas en esta ejecución.
