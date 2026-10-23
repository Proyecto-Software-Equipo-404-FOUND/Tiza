# H3 — Cancelación del registro institucional

Base: origin/main a8cab06. No incluye H1/H2 ni ID-07/08/09 y no agrega migraciones.

## Causa y recorrido

Register no recibía CancellationToken; RegisterHandler y los cuatro métodos de
IRegistrationDataAccess tampoco lo admitían. BeginTransactionAsync, la consulta con
FOR SHARE, la búsqueda global del correo y SaveChangesAsync se ejecutaban sin el token HTTP.

ASP.NET Core enlaza el nuevo parámetro CancellationToken de AuthController.Register con
HttpContext.RequestAborted, sin agregar parámetros al contrato HTTP. El controlador lo transmite
a RegisterHandler.HandleAsync y este a PuedeRegistrarAsync, ExisteEmailAsync, AddAsync y
SaveChangesAsync. Los valores opcionales default conservan compatibilidad de llamadas internas.
Infrastructure lo pasa a BeginTransactionAsync, ToListAsync, AnyAsync, AddAsync y SaveChangesAsync.
No cambian rutas, DTO, autorización, tenant, roles, normalización o manejo de conflictos.

BCrypt es síncrono y no admite cancelación: se comprueba antes y después del cálculo. No se
promete interrumpir el hash en curso. La cancelación se propaga como OperationCanceledException;
no se inventa un código HTTP para clientes desconectados ni se traduce a un error de negocio.

## Transacción y límite de confirmación

La comprobación de administrador inicia la transacción existente y mantiene sus locks hasta
confirmar o abandonar el registro. Una cancelación durante la consulta de autorización dispone
inmediatamente la transacción. Si el caso de uso termina antes de SaveChangesAsync (validación,
búsqueda o BCrypt), la instancia scoped IAsyncDisposable libera la transacción al cerrar el scope.
No hay INSERT antes del guardado. SaveChangesAsync dispone la transacción en finally incluso si
el token está cancelado; la disposición no recibe el token abortado.

Tras SaveChangesAsync se comprueba una última vez la cancelación. Hasta ese punto los INSERT,
triggers y reservas de correo permanecen dentro de la transacción exterior y pueden revertirse.
La confirmación empieza inmediatamente después de esa comprobación y usa CancellationToken.None:
una vez elegido confirmar, no se interrumpe el commit por desconexión HTTP. No se comprueba
cancelación después del commit para evitar comunicar como cancelado un registro ya confirmado.
Una cancelación que llegue después del límite puede dejar el alta completa aunque el cliente no
reciba el 201. No queda una escritura parcial. Fallos reales del commit siguen propagándose.
Una pérdida de conexión durante commit puede producir resultado indeterminado para el cliente;
no se introduce idempotencia pública ni se promete deshacer confirmaciones ya completadas.

Este límite se aplica al flujo autorizado que abrió la transacción en PuedeRegistrarAsync.
La disponibilidad de métodos separados en el puerto no habilita omitir validaciones del caso de uso.
No se cambia el esquema de transacciones ni se agrega una política de reintentos.

## Pruebas

RegistrationCancellationTests unitarias: mismo token en las cuatro dependencias; cancelación al
inicio, autorización, correo, hash, agregado y guardado; 201 normal; comprobación del RequestAborted
real del servidor; cancelación HTTP después de alcanzar una barrera en la dependencia, sin escritura.
También se verifica que CancellationToken no aparezca como parámetro HTTP en OpenAPI.
Los dobles históricos solo adaptan sus firmas; las aserciones anteriores permanecen.

RegistrationCancellationTests de integración: cancelar después del INSERT y del trigger de reserva,
antes de commit, revierte ambos registros; cancelar dentro del interceptor de commit demuestra que
se espera la confirmación completa; cancelar una consulta realmente bloqueada libera la transacción.
La cancelación durante el hash comprueba ausencia de INSERT y liberación de locks al cerrar el scope.
Se usa pg_stat_activity para observar la barrera y luego cancelar. El polling no ordena las carreras;
los timeouts son límites de seguridad, no sleeps usados para simular la cancelación.

PostgreSQL se valida exclusivamente en un contenedor desechable con datos tmpfs y sin puertos
publicados. El runner comparte su espacio de red; localhost:5541/pei_identidad_tests es su destino
interno aislado, sin acceso a la instancia persistente del host. Las migraciones históricas solo se
aplican en esquemas aleatorios de ese destino efímero. No se ejecuta el cargador de semillas.

Al integrar H2, conservar sus métodos nuevos del doble FakeIdentityStore y agregar las firmas
con CancellationToken de IRegistrationDataAccess. H3 no modifica IAuthenticationTokenWriter.
ID-07 no debe documentar CancellationToken como parámetro HTTP. ID-08/09 no requieren cambios.

## Resultado de validación

Compilación de PEI.Identidad.sln: 0 errores y 0 advertencias. Unitarias completas: 122
aprobadas, 0 fallidas, 0 omitidas. Integraciones completas en PostgreSQL 16 efímero: 52
aprobadas, 0 fallidas, 0 omitidas, incluidas cuatro nuevas H3. git diff --check correcto.
No se aplicaron migraciones ni semillas en bases persistentes. No se modificaron contratos
HTTP, rutas, claims, roles ni otras historias. Los cambios quedan sin staging, commit o push.
