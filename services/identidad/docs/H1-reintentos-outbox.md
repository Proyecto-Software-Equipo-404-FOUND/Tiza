# H1 — Programación de reintentos del outbox

Base: origin/main a8cab06. Cambio interno de Identidad, independiente de ID-07/08/09.

## Comportamiento

EntregaInvitacion.ProximoIntento es nullable y UTC (timestamp with time zone).
Una entrega pendiente es elegible cuando COALESCE(ProximoIntento, Creada) no supera
statement_timestamp() de PostgreSQL. Las filas existentes permanecen intactas y se
consideran inicialmente según Creada. Se ordena por elegibilidad, Creada e Id, manteniendo
FOR UPDATE SKIP LOCKED dentro de la transacción de selección/envío/guardado.

Después de un fallo del proveedor o descifrado, se programa desde clock_timestamp(),
consultado después del fallo: intento 1 espera 1 minuto, intento 2 espera 2 minutos,
hasta 60 minutos. Intentos se satura en int.MaxValue para evitar desbordamiento.
Ambas funciones de tiempo usan el reloj de PostgreSQL; statement_timestamp es estable
para la consulta y permite el rango del índice. No se usa la hora del proceso para
programar reintentos. El vencimiento funcional y el worker de diez segundos no cambian.

La entrega fallida queda temporalmente fuera de la selección; otras pendientes pueden avanzar.
Se conservan cifrado, propósito de Data Protection, identificador estable y limpieza de contenido
tras entrega o consumo. No se añade abandono definitivo ni se publican excepciones del proveedor.
La cancelación solicitada se propaga y no se convierte en un fallo reintentable confirmado.

## Persistencia y despliegue

Migración nueva: 20261022192629_ScheduleInvitationRetries, PlataformaDbContext.
Agrega solo la columna nullable y un índice parcial de expresión para entregas pendientes:
IX_EntregaInvitacion_PendienteElegible sobre COALESCE(ProximoIntento, Creada), Creada, Id.
EF Core no representa esa expresión como clave de índice: se mantiene explícitamente
mediante SQL Up/Down de esta migración. La columna sí está en configuración, designer y snapshot.

Up no actualiza, elimina ni sustituye filas existentes. La creación del índice normal puede
bloquear escrituras durante su construcción: planificar una ventana según el tamaño real de la tabla.
Down retira índice y columna, perdiendo solo la programación agregada por H1; no elimina entregas.
No modificar migraciones históricas ni aplicar automáticamente durante el arranque.
La versión nueva requiere la migración aplicada antes de procesar el outbox.

Al integrar ID-08, su comprobación de GetMigrations() exigirá esta migración adicional;
el cargador no la aplica. No cambiar sus protecciones ni sus datos semilla como parte de H1.

## Pruebas

OutboxRetryTests: equidad entre entregas, antigüedad sin reintento inmediato, fecha desde el fallo,
crecimiento y máximo, contador extremo, workers con contención controlada, rollback ante fallo
PostgreSQL, identificador estable y migración sobre filas existentes. Se usan barreras y fechas
explícitas; los timeouts son límites de seguridad, no el mecanismo de sincronización.
OutboxWorkerTests: el warning del worker no incluye la excepción ni su contenido confidencial.
La prueba histórica de reintento hace elegible ProximoIntento, sin alterar Creada para simularlo.

Validación PostgreSQL únicamente en un contenedor desechable con datos en tmpfs y sin puertos
publicados, mediante un runner en su espacio de red. No se conecta al PostgreSQL persistente local.
El destino interno conserva pei_identidad_tests en localhost:5541 y esquemas aleatorios por caso.
No se aplican migraciones ni se ejecutan pruebas de escritura en bases persistentes.

Un envío externo exitoso no se revierte con el rollback SQL. Ante fallo posterior de persistencia,
puede reenviarse el mismo mensaje: el proveedor debe deduplicar por el Id estable. No se promete
entrega exactamente una vez. La prueba de rollback verifica explícitamente esta condición.

## Resultado de validación

Compilación de PEI.Identidad.sln: 0 errores, 0 advertencias. Unitarias: 113 aprobadas.
Integración completa sobre PostgreSQL 16 efímero: 53 aprobadas, 0 fallidas, 0 omitidas,
incluidas las cinco pruebas nuevas H1. La generación offline del SQL confirma que Up solo
agrega columna, índice e historial de migración. No se aplicó sobre bases persistentes.
