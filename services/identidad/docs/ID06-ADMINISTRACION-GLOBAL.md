# ID-06 — Administración global

## Alcance y separación

`CuentaPlataforma` no es `Usuario`, no tiene institución y solo admite `AdministradorGlobal`.
La API institucional mantiene `Bearer`, su contrato obligatorio `institucion_id`, su política
predeterminada y `InstitutionAdmin`. Plataforma usa exclusivamente `PlatformBearer` y
`PlatformAdmin`, con clave, issuer y audience diferentes. El esquema local de desarrollo tampoco
puede compartir esa confianza. No se agregaron bypasses a `TenantContext` ni a `SaveChangesAsync`.

La funcionalidad está deshabilitada por defecto. El login institucional y sus respuestas no cambian.
Los puertos de refresh institucionales y su rotación permanecen intactos. El login global devuelve
solo access token: no hay refresh global, registro público global ni suplantación institucional.

## Configuración local

Ejecutar desde la raíz del repositorio con ASPNETCORE_ENVIRONMENT=Development. Configurar fuera
de Git mediante User Secrets de la API para desarrollo o un gestor de secretos. No introducir
contraseñas ni claves reales en argumentos, scripts, historial de consola o appsettings versionados.
User Secrets es almacenamiento local de desarrollo, no una bóveda productiva; restringir sus ACL.

Además de ConnectionStrings:IdentidadDb y Jwt institucional, configurar:

| Clave | Valor requerido |
|---|---|
| Platform:Enabled | true, habilitación explícita |
| Platform:Jwt:SecretKey | Clave aleatoria propia, al menos 32 bytes UTF-8 |
| Platform:Jwt:Issuer | Emisor distinto del institucional/local |
| Platform:Jwt:Audience | Audiencia distinta de la institucional/local |
| Platform:Jwt:AccessTokenMinutes | 1–60; predeterminado 15 |
| Platform:Jwt:ClockSkewSeconds | 0–60; predeterminado 15 |
| Platform:ActivationUrl | URL del cliente de activación, sin query ni fragmento |
| Platform:KeyDirectory | Directorio absoluto, fuera del repositorio, con ACL restringidas |
| Platform:DevelopmentMailDirectory | Directorio absoluto privado, fuera del repositorio |

La URL exige HTTPS, salvo HTTP loopback en Development. El cliente de activación no pertenece
a este servicio: debe leer `#token=...`, quitar el fragmento del navegador y enviar el token en
el cuerpo del POST de activación. No ponerlo en query strings ni en telemetría.

Las claves Data Protection deben persistir y conservarse entre reinicios. En Windows se protegen
con DPAPI del usuario que ejecuta el proceso. La API y las herramientas de consola deben usar
la misma identidad de SO, directorio y nombre de aplicación. Perder las claves impide recuperar
entregas pendientes. En otros sistemas, proteger el directorio con permisos exclusivos; antes de
producción configurar además la protección adecuada de las claves en reposo y copias seguras.

Fuera de Development, el arranque rechaza Platform si no se registró un IEmailSender productivo
antes de AddPlataforma. El buzón local nunca se habilita automáticamente en producción.

## Migraciones: no se aplican al arrancar

Dos contextos, misma base y mismo esquema:

1. IdentidadDbContext: InitialCreate → ProtectIdentityInvariants → NormalizeGlobalEmails.
2. PlataformaDbContext: InitialPlatformSchema → PlatformInvitations.

Plataforma utiliza __EFMigrationsHistoryPlataforma. Su primera migración exige la función de
normalización y los triggers institucionales. No aplicar plataforma en otra base o antes de la
cadena institucional. No usar EnsureCreated para sustituir migraciones.

`migracion-plataforma-revision.sql` se genera desde ambas migraciones de plataforma para revisión;
no incluye las migraciones institucionales ni debe ejecutarse indiscriminadamente. Cualquier
aplicación sobre una base de desarrollo/compartida requiere revisión y autorización del responsable.
Durante esta tarea solo se migran esquemas efímeros dentro de pei_identidad_tests.

InitialPlatformSchema aún no había sido aplicada: se corrigió esa migración pendiente. La nueva
PlatformInvitations crea invitaciones/outbox y objetos SQL operativos de auditoría/bootstrap.
La FK de invitación a Usuario se expresa en SQL para no incorporar Usuario al contexto global.
Auditoría/bootstrap son objetos SQL operativos, sin repositorios CRUD.

Las reversiones están bloqueadas si perderían reservas, cuentas, invitaciones o auditoría.
No hay una operación Down que borre silenciosamente esos datos. Un cambio de esquema con datos
reales requiere un plan explícito de conservación; no deshabilitar triggers para forzar el rollback.

## Bootstrap controlado

Configurar temporalmente PlatformBootstrap:Enabled=true y, mediante el proveedor de secretos,
PlatformBootstrap:Email, Password, Nombre, Apellido y Operador (identificador del operador autorizado).
Después de revisar/aplicar las migraciones al destino autorizado, ejecutar:

```powershell
dotnet run --project services/identidad/src/PEI.Identidad.Api --no-launch-profile -- --bootstrap-platform
```

El comando termina sin publicar HTTP ni arrancar el worker. Bloqueo asesor transaccional y una
marca persistente impiden inicializaciones concurrentes o repetidas, incluso si luego se elimina
la cuenta global. El marcador registra cuenta, operador y fecha UTC y rechaza UPDATE/DELETE/TRUNCATE.
Retirar los secretos de bootstrap y su habilitación inmediatamente después. No hay credenciales
predeterminadas ni token global de prueba. El operador debe estar autorizado también por los
controles del entorno: un campo Operador no sustituye la identidad y auditoría del gestor de secretos.

## Recorrido HTTP

1. POST /api/plataforma/auth/login: Email, Password. Respuesta accessToken y tokenType.
2. Usar ese bearer en POST /api/plataforma/instituciones con Nombre, Codigo,
   EmailAdministrador, NombreAdministrador, ApellidoAdministrador.
3. Respuesta 201 con institucionId y administradorId, sin token ni contraseña.
4. GET /api/plataforma/instituciones?pagina=1 devuelve metadatos institucionales, 50 por página.
   No consulta datos académicos ni expone hashes/usuarios.
5. El outbox procesa una entrega cada diez segundos y guarda un archivo .mail cifrado en el buzón local.
6. Tomar el GUID del nombre del archivo y configurar PlatformDevelopment:MailId. Leerlo localmente:

```powershell
dotnet run --project services/identidad/src/PEI.Identidad.Api --no-launch-profile -- --read-development-mail
```

Este comando de desarrollo muestra deliberadamente el mensaje descifrado en la terminal del operador;
no redirigirlo a logs, CI, archivos compartidos ni informes. No existe un endpoint de lectura del buzón.

7. POST /api/plataforma/invitaciones/activar con Token y Password (8 caracteres mínimo, 72 bytes
   UTF-8 máximo). Devuelve 204. El token es aleatorio de 256 bits, vence a las 24 horas y tiene un solo uso.
8. El administrador activado ingresa por /api/auth/login y registra Docente/Alumno mediante el flujo
   institucional existente. El token global no sirve en esos endpoints, ni el institucional en plataforma.

## Atomicidad, entrega y correos históricos

Institución, usuario inicialmente inactivo, invitación, entrega pendiente y auditoría se confirman en
una transacción. El actor global se vuelve a validar bajo lock. El rol del primer usuario siempre
es Administrador, fijado por el servidor; el cliente no puede elegir IDs, tenant ni rol.

El hash SHA-256 es la única representación usada para validar el token de invitación. Para permitir
reintentos de correo, el outbox conserva temporalmente el mensaje cifrado mediante Data Protection;
no conserva un token en claro. Borra ese contenido tras entrega, activación o detección de vencimiento.
El buzón local también está cifrado. No se registran cuerpos ni tokens.

Un fallo de correo no revierte la institución ya confirmada ni activa al usuario: deja una entrega
pendiente. El worker usa FOR UPDATE SKIP LOCKED y reintentos diferidos. La entrega externa es al menos
una vez: un proveedor productivo debe deduplicar usando MensajeEmail.Id. El buzón local lo hace por
nombre de archivo. No se promete exactly-once entre PostgreSQL y un proveedor externo.

El registro global reserva todos los correos usados por (TipoCuenta, CuentaId), sin FK de borrado
en cascada. Desactivar/eliminar no libera reservas; cambiar agrega otra y volver a una propia está
permitido. La PK arbitra conflictos concurrentes. Triggers bloquean UPDATE/DELETE/TRUNCATE y reservas
falsas o sin normalizar. Los índices institucionales existentes permanecen.

La comprobación previa de registro institucional sigue siendo orientativa; el registro compartido
y los triggers son la autoridad final. Sus conflictos se traducen a 409 genérico, sin revelar otra cuenta.
Los triggers no son una frontera contra el propietario/superusuario PostgreSQL, que puede deshabilitarlos:
usar roles de ejecución mínimos, separados del rol de migración, sin privilegios de DDL ni BYPASSRLS.

## Pruebas y límites

```powershell
dotnet build PEI.sln
dotnet test PEI.sln
```

Para PostgreSQL configurar PEI_IDENTIDAD_TEST_CONNECTION por un canal seguro. El fixture exige
pei_identidad_tests en localhost/127.0.0.1/::1:5541, valida current_database(), crea un esquema aleatorio,
aplica las cadenas en orden y elimina solamente ese esquema. Sin variable se omiten esas pruebas;
no interpretar la omisión como aprobación. No usar la base de desarrollo.

Las pruebas cubren autenticación real JWT/BCrypt, separación de esquemas, actividad de cuentas,
alta/activación HTTP con PostgreSQL, rollback tardío, backfill, historial, SQL directo, concurrencia,
bootstrap único, vencimiento/reuso y reintentos de correo, además de la regresión ID-05 existente.

Antes de un despliegue productivo: registrar proveedor real de email, proteger/copiar claves,
configurar cliente de activación y observabilidad sin secretos, revisar MFA y recuperación operativa.
No se implementan interfaz web, refresh global, reenvío de invitación vencida ni administración de
administradores globales adicionales: no se conceden esos permisos por inferencia.

## Auditoría de la implementación parcial

Se conservó la rama feature/ID-06-administracion-global y los cambios encontrados al comenzar.
La estructura de reserva histórica, el bloqueo de Usuario y el patrón INSERT ON CONFLICT DO NOTHING
seguido de comprobación de propietario eran aprovechables; no se reemplazaron por una consulta previa
vulnerable a concurrencia. Se encontraron y corrigieron:

- Inserciones directas de reservas sin verificar propietario real ni normalización.
- TRUNCATE podía eludir la protección basada únicamente en DELETE.
- La identidad primaria podía cambiar y desalinear la propiedad de reservas históricas.
- CuentaPlataforma carecía de recorte de email para SQL directo, aunque el cálculo normalizaba.
- Down eliminaba reservas históricas y cuentas sin impedir pérdida de información.
- La dependencia de los triggers institucionales no se comprobaba explícitamente.
- El mapeo de pei_normalize_email faltaba en PlataformaDbContext para consultas LINQ.
- RegistrationDataAccess solo traducía una restricción única institucional: los conflictos nuevos
  del registro compartido debían producir el mismo 409 genérico.
- Faltaban autenticación global, casos de uso, invitación/entrega diferida, activación y bootstrap.

## Inventario de archivos de esta tarea

Rutas relativas a services/identidad, salvo el SQL de la raíz.

Archivos nuevos:

- docs/ID06-ADMINISTRACION-GLOBAL.md
- src/PEI.Identidad.Api/Controllers/PlataformaController.cs
- src/PEI.Identidad.Api/Security/PlataformaSetup.cs
- src/PEI.Identidad.Application/Plataforma/ContratosPlataforma.cs
- src/PEI.Identidad.Application/Plataforma/PlataformaHandlers.cs
- src/PEI.Identidad.Domain/Entities/InvitacionAdministrador.cs
- src/PEI.Identidad.Infrastructure/Persistence/PlatformConfigurations/InvitacionConfiguration.cs
- src/PEI.Identidad.Infrastructure/Persistence/MigrationsPlataforma/20261021031817_PlatformInvitations.cs
- src/PEI.Identidad.Infrastructure/Persistence/MigrationsPlataforma/20261021031817_PlatformInvitations.Designer.cs
- src/PEI.Identidad.Infrastructure/Plataforma/PlataformaDataAccess.cs
- src/PEI.Identidad.Infrastructure/Plataforma/PlataformaBootstrap.cs
- src/PEI.Identidad.Infrastructure/Plataforma/EntregaInvitaciones.cs
- src/PEI.Identidad.Infrastructure/Security/PlataformaSecurity.cs
- tests/PEI.Identidad.IntegrationTests/PlataformaPersistenceTests.cs
- tests/PEI.Identidad.IntegrationTests/PlataformaHttpFlowTests.cs
- tests/PEI.Identidad.UnitTests/PlataformaHttpTests.cs
- tests/PEI.Identidad.UnitTests/PlataformaDeliveryTests.cs

Archivos completados/modificados, incluyendo los que ya estaban sin seguimiento:

- src/PEI.Identidad.Api/Program.cs
- src/PEI.Identidad.Infrastructure/Persistence/IdentidadDbContext.cs
- src/PEI.Identidad.Infrastructure/Persistence/PlataformaDbContext.cs
- src/PEI.Identidad.Infrastructure/Repositories/RegistrationDataAccess.cs
- src/PEI.Identidad.Infrastructure/Persistence/MigrationsPlataforma/20261020231100_InitialPlatformSchema.cs
- src/PEI.Identidad.Infrastructure/Persistence/MigrationsPlataforma/PlataformaDbContextModelSnapshot.cs
- migracion-plataforma-revision.sql (raíz del repositorio, regenerado).

Se conservaron RegistroEmail.cs, su configuración, la factory de plataforma y el designer inicial,
que ya estaban sin seguimiento; CuentaPlataforma y su configuración ya estaban confirmadas en Git.
No se modificaron otros microservicios, infra/docker-compose.yml ni .github. Staging sin cambios.

## Resultado de verificación — 21 de octubre de 2026

- dotnet build PEI.sln --no-restore: correcto, 0 errores, 0 advertencias.
- dotnet test PEI.sln --no-build: 169 correctas, 0 fallidas, 0 omitidas.
- Identidad: 92 pruebas unitarias/HTTP y 38 de integración con PostgreSQL real.
- Otros servicios: 39 pruebas correctas.
- Ambos modelos EF coinciden con sus snapshots; comprobación de plataforma adicional con dotnet ef.
- Script de plataforma regenerado, sin aplicarlo a desarrollo; git diff --check correcto.

Solo se modificaron esquemas aleatorios del fixture en pei_identidad_tests. No se ejecutó el bootstrap
contra datos de desarrollo ni se utilizaron credenciales reales en el código. No se probó un proveedor
de correo externo ni un despliegue productivo, porque no forman parte del entorno disponible.

Recomendación: apto para revisión técnica y preparación de commit por el responsable. El flujo solicitado
está implementado y probado con el proveedor de desarrollo; no confundirlo con habilitación productiva
sin los requisitos operativos enumerados arriba. No se hicieron commits, push ni cambios al staging.
