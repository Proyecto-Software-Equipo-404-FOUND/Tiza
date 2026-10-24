# Integracion local de Identidad

## Entorno y procedencia

Rama: integration/identidad-id01-id03-id05. Base: ID-05, 51031a6.
Funcionalidades trasladadas de forma controlada desde ID-03 aeb5d52 e ID-01 77c0310. No se ejecutaron merges, rebase, commits ni push. El trabajo esta en el working tree, incluidos archivos nuevos sin seguimiento; la rama sigue apuntando a la base hasta que el responsable prepare un commit.

## Registro administrativo

POST /api/auth/register exige un JWT valido y politica InstitutionAdmin. El DTO contiene Email, Password, Nombre, Apellido y Rol, sin InstitucionId. Los campos JSON desconocidos se rechazan con 400, por lo que enviar una institucion no permite elegir otra. Solo se admiten Docente y Alumno, con capitalizacion canonica. Administrador, Usuario y otros roles se rechazan.

El handler toma la institucion del ITenantContext de ID-05. La infraestructura comprueba al administrador real y la institucion activos, tomando locks compartidos en una transaccion hasta guardar. Las validaciones originales de SaveChangesAsync siguen vigentes. La comprobacion global de email incluye cuentas inactivas y utiliza la funcion PostgreSQL existente. El indice unico es la autoridad final; la violacion 23505 de IX_Usuario_EmailNormalizado produce 409 generico sin identificar otra institucion. El repositorio generico GetByEmail de ID-03 no se traslado porque no es adecuado para autenticacion ni unicidad global bajo filtros.

No se implemento un endpoint de aprovisionamiento. El primer administrador debe existir mediante un procedimiento externo aprobado, autenticado y auditado. No se convierten roles existentes automaticamente: las cuentas legacy con rol Usuario necesitan una decision administrativa de asignacion de rol y no pueden autenticarse como un rol canonico por simple equivalencia.

## Login, claves y claims

POST /api/auth/login permanece anonimo, utiliza BuscarUsuarioParaLoginAsync y no depende de Usuario.Institucion cargado. La consulta acotada de ID-05 garantiza actividad. BCrypt real verifica el hash; el usuario inexistente/inactivo recorre un hash ficticio y los hashes corruptos generan un fallo controlado. La respuesta es 401 generico. La limitacion por IP devuelve 429 despues de 10 intentos por minuto, configurable mediante LoginRateLimit:PermitLimit. En despliegues con proxy, revisar los proxies confiables antes de adoptar forwarded headers; no se confia automaticamente en un X-Forwarded-For enviado por el cliente.

Los nuevos hashes usan BCrypt costo 12 y salt generado por la biblioteca. Se admiten contrasenas de al menos 8 caracteres y hasta 72 bytes UTF-8 para registro. Los hashes anteriores siguen siendo verificables; no se migro ni rehasheo la base. No se promete latencia constante con distintos costos de hashes legacy: la verificacion ficticia y el rate limiting son defensas complementarias.

Configurar fuera del repositorio:

- Jwt:SecretKey: clave aleatoria criptografica representada en UTF-8, al menos 32 bytes. No usar una frase predecible ni una clave de produccion en desarrollo.
- Jwt:Issuer y Jwt:Audience: valores explicitos no vacios.
- Jwt:AccessTokenMinutes: 1 a 60, default 15.
- Jwt:ClockSkewSeconds: 0 a 60, default 15.
- ConnectionStrings:IdentidadDb: conexion del entorno, nunca credenciales versionadas.

Se conserva UserSecretsId de ID-05/ID-03. Se elimina la ambiguedad Jwt:Secret; solo SecretKey configura el emisor ordinario. La codificacion de la clave es UTF-8 tanto al firmar como al validar.

JWT: sub e institucion_id contienen GUIDs no vacios; role es Administrador, Docente o Alumno. Se emiten jti, iat y email opcional. MapInboundClaims=false, NameClaimType=sub y RoleClaimType=role. Se rechazan claims obligatorios ausentes o repetidos, roles desconocidos, GUIDs invalidos/vacios y aliases anteriores de identidad. Firma HS256, issuer, audience y expiracion son obligatorios. Los tokens anteriores con claims InstitucionId/institucionId requieren volver a iniciar sesion; no hay aliases ambiguos de compatibilidad.

Cada JWT autenticado tambien se contrasta con usuario, institucion y rol actuales activos. Esta consulta privilegiada esta acotada a esa identidad completa y no altera los filtros del contexto. No hay cache: implica una lectura PostgreSQL por autenticacion. Si PostgreSQL no esta disponible, no se concede acceso mediante un fallback permisivo.

## JWT de prueba: consola, sin endpoint publico

No existe POST /api/v1/tokens/prueba en el producto integrado. La capacidad de ID-01 se conserva mediante:

    dotnet run --project services/identidad/src/PEI.Identidad.Api --no-launch-profile -- --generate-test-token

La herramienta retorna antes de construir/publicar el servidor. Requiere ASPNETCORE_ENVIRONMENT=Development y DevelopmentTools:EnableTestTokens=true. Configurar externamente DevelopmentTools:UsuarioId, InstitucionId, Rol y la seccion DevelopmentTools:Jwt con SecretKey, Issuer, Audience y duracion. Rol y GUIDs cumplen el mismo contrato canonico.

La clave y el issuer locales deben ser distintos de los ordinarios. No copiar claves productivas. El token se escribe en consola deliberadamente: no guardarlo en Git, logs compartidos ni informes. Por defecto esta herramienta y su confianza estan deshabilitadas. Staging y Production rechazan la habilitacion.

Solo si se habilita expresamente en Development, la API registra confianza adicional para esos tokens locales. Las direcciones de escucha efectivas (Kestrel:Endpoints o urls) deben ser explicitas y exclusivamente loopback; se rechazan comodines y direcciones publicas. No publicar esta instancia mediante un reverse proxy ni compartir las claves locales. Los usuarios/instituciones/roles del token deben existir y estar activos tambien para ese esquema; no se otorgan permisos a IDs ficticios solo por estar en desarrollo. Puede usarse la herramienta por separado para otros consumidores locales de prueba, sin habilitar esta confianza en el servicio.

Se conserva la trazabilidad de ID-03 mediante X-Correlation-ID, scope de logging y registro de metodo/path, sin registrar cuerpos ni credenciales.

## Persistencia y dependencias

No se modificaron el contexto, filtros, ITenantContext, TenantContext, accesos ID-05 de autenticacion/refresh, configuraciones, entidades existentes, migraciones ni snapshot. No hay migracion nueva; EF confirma que el modelo coincide con el snapshot. Los triggers, normalizacion, unicidad y xmin siguen siendo los existentes. No se agregaron endpoints de renovacion/logout ni cambios en la rotacion transaccional.

JwtBearer y Mvc.Testing: 8.0.32. System.IdentityModel.Tokens.Jwt y Protocols.OpenIdConnect: 8.23.0, con familia IdentityModel resuelta coherente. BCrypt.Net-Next: 4.2.0. EF y Npgsql permanecen en sus versiones ID-05. La consulta de vulnerabilidades NuGet no informo paquetes vulnerables en las fuentes configuradas; no equivale a ausencia de todo riesgo.

## Pruebas y revision

Se conservan los 15 casos unitarios y 20 de persistencia de ID-05. Las ocho pruebas de handlers de ID-03 se trasladaron desde UnitTest1.cs a AuthHandlerTests.cs y se adaptaron a registro administrativo y login acotado; no se restauro la plantilla eliminada. Se agregaron casos HTTP mediante TestServer/WebApplicationFactory con JwtBearer real y BCrypt real, usando dobles acotados de acceso a datos para las pruebas que no necesitan PostgreSQL. El controlador TenantProbe existe solo en el ensamblado de pruebas y nunca se registra por el producto.

AdministrativeHttpTests agrega pruebas con PostgreSQL real para login, registro institucional, rechazo de institucion en el cuerpo, unicidad dentro/fuera del tenant, concurrencia y actividad institucional. Reutiliza el fixture seguro de ID-05, que valida la base pei_identidad_tests local:5541 antes de crear esquemas aislados. No usar pei_identidad_db.

Ejecutar desde la raiz del worktree:

    dotnet build PEI.sln
    dotnet test PEI.sln
    dotnet list services/identidad/PEI.Identidad.sln package --vulnerable --include-transitive

Sin PEI_IDENTIDAD_TEST_CONNECTION se omiten los casos PostgreSQL: no estan aprobados. No se aplicaron migraciones a bases existentes durante esta integracion. El cierre requiere ejecutar esos casos con la conexion autorizada y revisar la politica de aprovisionamiento con el equipo.

Para revisar el codigo antes de preparar integraciones de PR:

    git status --short
    git diff --check
    git diff --stat
    git diff -- services/identidad
    git ls-files --others --exclude-standard

Git diff no incluye el contenido de archivos nuevos sin seguimiento: abrirlos tambien, especialmente Security, handlers, controlador, repositorio y pruebas. No fusionar automaticamente las ramas originales: sus handlers/configuraciones previas son incompatibles con este contrato y necesitan la adaptacion aqui documentada. No se modificaron main, las ramas originales ni sus referencias remotas. El checkout principal y sus cambios ajenos se conservaron. El staging del worktree permanece vacio.
