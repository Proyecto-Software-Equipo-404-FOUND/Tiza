# ID-08 — Datos semilla de Identidad con UUIDs fijos

## Alcance y base

Catálogo sintético `ID08-v1`: dos instituciones activas y ocho usuarios activos.
Implementado sobre `2eb588bfe669b363d41287009016845a7710b899`, por autorización expresa;
depende del modelo institucional y de las migraciones de plataforma de esa integración.
La rama no debe confundirse con una implementación independiente del estado actual de main.

La herramienta de consola está en `tools/PEI.Identidad.DevelopmentSeed`. No publica HTTP,
no se registra en la API, no se ejecuta al iniciar servicios y no aplica migraciones.
No modifica login, registro, JWT, refresh/logout, bootstrap, invitaciones ni aprovisionamiento productivo.
Los administradores activos de este catálogo son exclusivamente cuentas de desarrollo/pruebas;
el alta productiva conserva la invitación y activación de ID-06.

## Catálogo estable

| Institución | Nombre | Código | UUID |
|---|---|---|---|
| A | Institución de prueba A | TIZA-TEST-A | 08000000-0000-4000-8000-000000000001 |
| B | Institución de prueba B | TIZA-TEST-B | 08000000-0000-4000-8000-000000000002 |

| Institución | Rol | Correo | UUID |
|---|---|---|---|
| A | Administrador | admin@a.test | 08000001-0000-4000-8000-000000000001 |
| A | Docente | docente@a.test | 08000001-0000-4000-8000-000000000002 |
| A | Alumno | alumno1@a.test | 08000001-0000-4000-8000-000000000003 |
| A | Alumno | alumno2@a.test | 08000001-0000-4000-8000-000000000004 |
| B | Administrador | admin@b.test | 08000002-0000-4000-8000-000000000001 |
| B | Docente | docente@b.test | 08000002-0000-4000-8000-000000000002 |
| B | Alumno | alumno1@b.test | 08000002-0000-4000-8000-000000000003 |
| B | Alumno | alumno2@b.test | 08000002-0000-4000-8000-000000000004 |

Cada usuario referencia el UUID de su institución. Los nombres son Administrador, Docente,
Alumno uno o Alumno dos; los apellidos son Prueba A o Prueba B. Se reutiliza IdentityRoles.
El formato de UUID facilita la documentación; nunca es fuente de permisos.

## Diseño y seguridad

- `SeedCatalog`: registros inmutables y listas de solo lectura, sin contraseñas.
- `SeedOptions`: configuración explícita, lista de destinos y credenciales externas por UUID.
- `IdentitySeedRunner`: una transacción, advisory lock `20061008`, preflight, clasificación e inserción.
- `Program`: entrada por variables de entorno, salida controlada y códigos 0 (éxito), 2 (rechazo)
  o 1 (error técnico). No admite argumentos de consola, conexiones ni contraseñas por argumentos.

Requiere entorno exactamente Development o Testing, habilitación explícita y operador.
Exige coincidencia de host, puerto, base y esquema con la lista autorizada; no hay destinos por defecto.
Verifica `current_database()`, `current_schema()` y `session_replication_role=origin` dentro de la transacción.
Usa un esquema dedicado, sin fallback a public; coloca pg_temp al final del search_path para
evitar que tablas temporales oculten las tablas reales. Los identificadores de esquema se restringen
a letras minúsculas, números y guion bajo (máximo 63 caracteres); las demás entradas son parámetros SQL.

Comprueba columnas/tipos/nulabilidad, columnas generadas, índices únicos válidos, restricciones,
relaciones FK y migraciones de ambos contextos contra los modelos EF existentes. Comprueba también
los triggers habilitados y sus funciones en el esquema esperado, las tablas operativas de ID-06
y una prueba de normalización. No desactiva triggers, no modifica el TenantContext y no utiliza HasData.
La verificación presupone migraciones confiables; no certifica criptográficamente el cuerpo de cada función SQL.

Se bloquean las tablas institucionales y de plataforma con SHARE ROW EXCLUSIVE durante la carga,
para estabilizar filas y reservas frente a escrituras concurrentes. Se repite el preflight tras los locks.
Hay límites de 15 segundos para esperar locks y 30 segundos por sentencia. Ante contención prolongada,
el operador debe reintentar. Esta herramienta no debe usarse contra servicios con tráfico real.

## Configuración externa

No se usa appsettings de la API ni su conexión habitual. No hay contraseña ni conexión predeterminada.
Proveer valores mediante un gestor de secretos o variables del proceso. No guardar sus valores en Git,
scripts versionados, historial de comandos, logs ni archivos de configuración compartidos.

| Variable | Contenido |
|---|---|
| ID08_ENVIRONMENT | Development o Testing |
| ID08_ENABLED | Literal `true` |
| ID08_OPERATOR | Identificador del operador, sin caracteres de control |
| ID08_SCHEMA | Esquema dedicado existente y autorizado |
| ID08_ALLOWED_DESTINATIONS | Array JSON de objetos con Host, Port, Database y Schema; coincidencia exacta |
| PEI_IDENTIDAD_TEST_CONNECTION | Conexión exclusiva en Testing |
| ID08_CONNECTION | Conexión explícita en Development; no se toma de la API |
| ID08_PASSWORD_&lt;UUID sin guiones&gt; | Contraseña externa del usuario correspondiente; solo para nuevos usuarios |

Ejemplo de **estructura** de destino de pruebas (reemplazar el esquema por el efímero autorizado):

```json
[{"Host":"localhost","Port":5541,"Database":"pei_identidad_tests","Schema":"pei_test_UUID_DE_LA_PRUEBA"}]
```

El marcador anterior no es un esquema utilizable. Usar el nombre minúsculo generado por el fixture.
Por ejemplo, la clave de configuración del administrador A es
`ID08_PASSWORD_08000001000040008000000000000001`. Las otras siete claves se derivan de la tabla.
No hay un valor de contraseña documentado ni incorporado al catálogo.

Reutiliza IPasswordHasher/BCryptPasswordHasher: costo 12, mínimo ocho caracteres y máximo 72 bytes UTF-8.
Cada nueva cuenta recibe un hash con sal independiente; la contraseña nunca se registra ni se devuelve.
La herramienta deshabilita detalles de error y logging de parámetros de Npgsql. Los errores técnicos
no imprimen excepciones, filas, hashes ni conexiones. Los conflictos solo identifican UUIDs del catálogo.

**Desarrollo:** el destino dedicado sigue pendiente de aprobación del equipo. No se habilita ni se crea
ninguna base de desarrollo con esta historia. Una lista vacía impide ejecutar. No usar esta herramienta
contra producción o staging; etiquetar una conexión productiva como Development no la vuelve segura.
El control de despliegue y el suministro de la lista autorizada deben permanecer bajo control del operador.

## Ejecución explícita

Únicamente después de configurar externamente las variables, aprobar el destino y disponer de las
migraciones institucionales y de plataforma aplicadas por el procedimiento correspondiente:

```powershell
dotnet run --project services/identidad/tools/PEI.Identidad.DevelopmentSeed --no-launch-profile
```

No hay opción para migrar, limpiar, resetear contraseñas o forzar conflictos.
El proceso termina después de informar registros creados, omitidos y conflictos.
Retirar las credenciales del entorno al terminar y conservar el registro operativo sin secretos.

Para esta implementación solo se autorizó ejecutar el cargador dentro de las pruebas automatizadas,
con esquemas efímeros en pei_identidad_tests local:5541. El comando anterior documenta la interfaz,
no autoriza cargas manuales en otras bases.

## Idempotencia y conflictos

Un registro es nuevo, existente compatible o conflictivo. Antes del primer INSERT se revisan los diez
UUIDs, códigos, correos normalizados, reservas históricas, nombres, apellidos, roles, estados y relaciones.
Las cuentas existentes deben conservar un hash con formato BCrypt y costo mínimo 12; no se exige
su contraseña para reejecutar. Las reservas deben apuntar a la misma identidad institucional existente.

Solo se insertan registros nuevos. Nunca se actualizan ni eliminan filas existentes. Las fechas de
creación se asignan en UTC una sola vez; una reejecución no cambia filas, hashes, fechas ni xmin.
La reproducibilidad corresponde a las identidades y relaciones, no al hash aleatorio ni a la fecha inicial.

Los triggers crean las ocho reservas institucionales. No se crean cuentas globales, refresh tokens,
invitaciones, outbox ni eventos atribuidos a actores globales ficticios. Los datos ajenos se conservan.
Una carga parcial compatible se completa; cualquier conflicto cancela la transacción completa.

Ejemplos de conflicto:

- TIZA-TEST-A pertenece a otro UUID: detenerse y revisar manualmente el destino/catalogación.
- ADMIN@A.TEST ya corresponde a otra cuenta: detenerse, incluso si solo cambia el uso de mayúsculas o espacios.
- Una cuenta fue eliminada, pero su correo sigue reservado: no recrearla ni borrar la reserva histórica.
- Un usuario del catálogo fue desactivado, cambió de rol o pertenece a otra institución: informar la
  divergencia; no reactivarlo, trasladarlo ni corregirlo automáticamente.

La resolución requiere una decisión del equipo y un procedimiento separado. Nunca eliminar datos,
reasignar UUIDs ni modificar protecciones para lograr que pase el seed. No hay limpieza automática
de datos sembrados: los tests eliminan únicamente el esquema efímero creado por su propio fixture.

## Pruebas y validación

Las pruebas existentes siguen usando sus propios fixtures y no dependen del catálogo precargado.
Las nuevas pruebas reutilizan TenantIsolationTests: cada caso crea un esquema aleatorio y valida
host local, puerto 5541, base pei_identidad_tests y destino real. Solo en ese esquema se aplican
migraciones durante el arreglo de la prueba; el cargador nunca las ejecuta.
El fixture y el comando deshabilitan pooling: cada esquema efímero produciría un pool distinto
y retendría conexiones ociosas. Al disponer las conexiones se liberan físicamente; esto evita
agotar max_connections al ejecutar la suite completa sin alterar la configuración del servidor.

Proveer PEI_IDENTIDAD_TEST_CONNECTION externamente y ejecutar desde la raíz:

```powershell
dotnet build PEI.sln
dotnet test PEI.sln --no-build --no-restore
dotnet test services/identidad/PEI.Identidad.sln --no-build --no-restore --filter "FullyQualifiedName~ID08_|FullyQualifiedName~IdentitySeedTests"
```

Sin PEI_IDENTIDAD_TEST_CONNECTION los casos PostgreSQL se omiten: eso no equivale a haberlos aprobado.
Las credenciales de los usuarios del seed se generan aleatoriamente dentro de las pruebas.
Se verifican catálogo, BCrypt, segunda ejecución sin credenciales, snapshot de filas/fechas/hashes/xmin,
carga parcial, colisiones, reservas históricas/globales, rollback de la última inserción, ausencia de
efectos laterales, registros ajenos, aislamiento y rechazo de escrituras cruzadas.
La prueba de concurrencia observa ambas sesiones bloqueadas en pg_stat_activity antes de liberar
el advisory lock. El usuario PostgreSQL debe poder observar sus propias sesiones.

## Limitaciones y revisión

El destino de desarrollo está pendiente. La rama depende del commit de integración aún sujeto a
coordinación del equipo; revalidar al trasladar a main. No se agregan migraciones ni privilegios de
base nuevos. El usuario ejecutor necesita permisos de lectura, inserción, locks y catálogos para
el esquema aprobado. No se incluyen secretos ni cambios productivos de autenticación.

## Resultado de validación local — 2026-10-21

`dotnet build PEI.sln --no-restore`: 0 errores y 0 advertencias.

`dotnet test PEI.sln --no-build --no-restore`:

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Identidad unitarias/HTTP | 125 | 0 | 0 |
| Identidad PostgreSQL | 63 | 0 | 0 |
| Otros microservicios | 39 | 0 | 0 |
| Total | 227 | 0 | 0 |

ID-08 incorpora 28 casos: 13 unitarios y 15 de integración PostgreSQL. La ejecución focalizada
utiliza el filtro indicado arriba. No se ejecutaron cargas sobre desarrollo ni producción.

Durante la primera ejecución completa se detectó agotamiento de conexiones (cinco fallos 53300).
La corrección se limita a deshabilitar pooling en el fixture efímero y el comando independiente;
la repetición completa pasó las 227 pruebas. No se alteró max_connections ni se terminaron sesiones ajenas.

La revisión local del diff no detectó secretos reales ni artefactos incorporados. Las soluciones
y las referencias de los dos proyectos de prueba incorporan el comando; el único cambio en
un fixture previo es la liberación física de conexiones. No se modificaron código productivo,
migraciones, ramas originales, staging ni Pull Requests. Los cambios quedan sin commit para revisión.
