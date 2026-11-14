# S2-ID-02 (#135) — Importación masiva de alumnos

## Alcance y estado del contrato

Este documento describe el contrato implementado por Identidad para importar alumnos desde CSV, invitarlos a definir su contraseña y activar sus cuentas. No declara implementada la pantalla de activación del frontend ni confirma que exista un proveedor productivo de correo o una configuración productiva de despliegue.

## Importación CSV

### Solicitud

```http
POST /api/usuarios/alumnos/importacion
Content-Type: multipart/form-data
Authorization: Bearer <JWT institucional>
```

El archivo debe enviarse en el campo multipart `archivo`. La operación está autorizada exclusivamente para el rol institucional `Administrador`. La institución se obtiene del contexto construido desde el JWT autenticado; no se acepta una institución desde el CSV, un campo adicional ni un header.

El contenido es CSV UTF-8, con BOM opcional, separador coma y encabezado exacto:

```csv
nombre,apellido,email
Ana,Pérez,ana@example.test
```

Se admiten las reglas CSV para comillas, comas y saltos de línea dentro de campos entrecomillados. El archivo debe contener como máximo 1 MiB y 200 filas de datos. Un archivo vacío, UTF-8 inválido, encabezado incorrecto, registro estructuralmente malformado o que exceda alguno de esos límites se rechaza completo antes de insertar filas. El tamaño del archivo que llega a la acción y supera 1 MiB responde 400; un cuerpo multipart que excede el límite total del servidor puede rechazarse con 413.

No se aceptan contraseñas. Cada fila válida intenta crear un usuario con rol `Alumno`, inicialmente inactivo y pendiente de activación. No se crean docentes ni se inscribe al alumno en cohortes.

### Respuesta y errores por fila

Una importación procesada responde 200 con cantidades y errores por fila. Los números de fila incluyen el encabezado como fila 1:

```json
{
  "creados": 1,
  "omitidos": 1,
  "errores": [
    { "fila": 4, "motivo": "El email no tiene un formato válido." }
  ]
}
```

Antes de validar cada fila, se recortan los espacios iniciales y finales de nombre, apellido y email. El nombre y el apellido son obligatorios y admiten hasta 100 caracteres cada uno. El email es obligatorio, admite hasta 150 caracteres y debe cumplir la validación `EmailAddressAttribute`. Los errores aislados de validación se informan y el procesamiento continúa con las demás filas. Los emails existentes se cuentan como omitidos; también se omiten las repeticiones normalizadas dentro del archivo, incluida una repetición posterior a un fallo de persistencia de la primera aparición. La normalización usa la función PostgreSQL `pei_normalize_email`. La respuesta no explica si un email omitido existe en otra institución.

Determinados errores no transitorios durante la persistencia se informan como error de fila y permiten continuar. No todo error técnico se trata de ese modo: `UnauthorizedAccessException` e `InvalidOperationException` se propagan y no se convierten en errores individuales de fila. Los errores de conexión que el handler identifica como transitorios o de transporte abortan la operación restante. Dentro de la transacción de cada fila, Identidad vuelve a validar que el administrador autenticado conserve el rol `Administrador`, que esté activo y que la institución del contexto siga activa. Cada fila creada se guarda de modo atómico: usuario, invitación y entrega Outbox se confirman en una única transacción. Si falla esa persistencia, se revierte la fila completa.

## Invitación y activación institucional

La invitación es de un solo uso y vence a las 24 horas. El token se genera en Identidad; el Outbox conserva su mensaje cifrado para entrega/reintento, no el token en claro. El enlace se forma con la opción independiente `Institutional:ActivationUrl` y el fragmento:

```text
<Institutional:ActivationUrl>#token=<token>
```

La URL configurada no debe incluir query ni fragmento. En producción debe ser una URL absoluta HTTPS válida. HTTP se permite solamente en loopback durante Development. La opción no se valida al iniciar la aplicación. Si falta o es inválida, `GetActivationUri()` lanza `InvalidOperationException` cuando se construye `ImportacionAlumnosDataAccess`, lo que impide realizar correctamente la importación. No hay un valor productivo definido en este documento: la configuración de cada entorno debe coordinarse con sus responsables de despliegue.

El endpoint institucional es:

```http
POST /api/auth/invitaciones/alumnos/activar
Content-Type: application/json
```

Es anónimo (`AllowAnonymous`) y está protegido por la política de rate limiting `login`; el código no define aquí un límite numérico.

```json
{
  "token": "<token leído del fragmento>",
  "password": "<contraseña elegida por el alumno>"
}
```

El comando valida los campos y la política de contraseña existente. Identidad valida el token, vencimiento, uso único, que la cuenta siga pendiente, que su rol real en la base sea `Alumno` y que la institución esté activa. El cliente no puede elegir el rol. La activación y el consumo de la invitación se realizan transaccionalmente; una activación posterior del mismo token es rechazada.

## Contrato y tareas del frontend

Identidad es responsable de generar el enlace y exponer el endpoint de activación. El frontend debe proporcionar la pantalla pública en la URL configurada, leer el token desde `window.location.hash` (`#token=...`), retirar el fragmento de la dirección cuando corresponda y enviar `token` y `password` al endpoint de Identidad. La pantalla debe permitir que el alumno defina su propia contraseña; Identidad no crea contraseñas temporales ni las recibe en el CSV.

La revisión del repositorio para S2-ID-02 no encontró una pantalla frontend de activación que implemente este flujo. La existencia del endpoint de backend no implica que la pantalla esté disponible. El equipo frontend debe coordinar además cómo se enruta la llamada hacia Identidad en cada entorno.

## Outbox y entrega de correo

La creación de la invitación persiste una entrega pendiente en el Outbox dentro de la misma transacción que el usuario y la invitación. Persistir la entrega significa que el mensaje está encolado; no significa que el correo haya sido enviado ni recibido.

En Development se registra `DevelopmentEmailSender`, que guarda mensajes en el buzón local cifrado. No se encontró una implementación productiva de `IEmailSender` en el repositorio. No debe afirmarse que Identidad envía correo real en producción hasta que el equipo decida y configure un proveedor.

El worker revisa el Outbox periódicamente. Si no encuentra un `IEmailSender` registrado, espera y conserva las entregas pendientes sin procesarlas. Cuando un sender está registrado y disponible, procesa una entrega elegible, descifra el contenido, intenta enviarlo y registra la entrega; ante un fallo conserva el mensaje y programa reintento. Las entregas vencidas o cuya invitación ya fue utilizada se limpian en lugar de enviarse. La entrega externa puede ser al menos una vez; un proveedor futuro debe considerar la idempotencia por identificador de mensaje.

## Data Protection y operación

Los cuerpos de invitación pendientes están cifrados mediante ASP.NET Core Data Protection. Las claves del key ring deben persistir y conservarse entre reinicios y estar disponibles para todas las instancias que deban procesar esos mensajes. Esto es necesario incluso con `Platform:Enabled=false`: deshabilitar las rutas y autenticación globales no deshabilita el worker ni elimina su dependencia criptográfica.

`Platform:KeyDirectory` se usa como ubicación explícita cuando está configurado; el código actual no lo exige cuando `Platform:Enabled=false`. Si no hay ubicación explícita, no se puede inferir del repositorio que el almacenamiento predeterminado del entorno sea durable o compartido. La ubicación, persistencia, protección y permisos de las claves quedan pendientes de coordinación con los responsables del despliegue. No se especifican aquí rutas ni valores productivos.

Si se pierden las claves que cifraron entregas pendientes, Data Protection no puede descifrarlas. El procesamiento incrementa el contador de intentos y programa el próximo reintento, pero no garantiza que el detalle del error interno de descifrado quede registrado en logs. Los reintentos no recuperan las claves ni el contenido; restaurar el key ring original es necesario para recuperar esos mensajes.



## Responsabilidades y dependencias

- **Identidad (S2-ID-02):** implementar y mantener el endpoint de importación, las validaciones, la creación de alumnos, las invitaciones, la activación institucional y la persistencia transaccional mediante Outbox.

- **Frontend:** implementar la pantalla de activación, leer el token del fragmento `#token=...`, permitir que el alumno defina su contraseña y enviar ambos datos al endpoint de Identidad.

- **Configuración del despliegue:** antes de habilitar la funcionalidad en producción, se deben proporcionar una `Institutional:ActivationUrl` HTTPS válida y un almacenamiento persistente y protegido para las claves de Data Protection, incluso cuando `Platform:Enabled=false`. Estas configuraciones todavía no están definidas en el repositorio.

- **Correo electrónico:** actualmente no hay un proveedor productivo de `IEmailSender` implementado. La entrega queda pendiente en Outbox hasta que se configure uno. La selección e integración de ese proveedor queda fuera del alcance de S2-ID-02.



## Documentación anterior que requiere contexto actualizado

Los documentos existentes se conservan sin cambios. Para interpretar S2-ID-02 junto con ellos, tener en cuenta:

- `ID06-ADMINISTRACION-GLOBAL.md` documenta el flujo global de invitaciones y el patrón de fragmento `#token=...`, pero no el endpoint institucional de alumnos ni la importación CSV. Su afirmación de que fuera de Development Identidad rechaza el arranque si no hay `IEmailSender` productivo ya no describe el comportamiento actual del Outbox: el worker puede arrancar sin sender y dejar pendientes las entregas.
- `ID07-SWAGGER-POSTMAN.md` enumera las operaciones documentadas antes de los endpoints institucionales de activación e importación. Su tabla no incluye esos endpoints y no debe tomarse como el inventario completo de S2-ID-02.
- `ID-09-configuracion-desarrollo.md` describe el recorrido local básico con `Platform:Enabled=false` y sin configuración de plataforma global. Eso no elimina la necesidad de conservar las claves de Data Protection para el Outbox institucional.
- `H1-reintentos-outbox.md` describe reintentos del Outbox, pero no el contrato de importación CSV ni la nueva separación entre el worker de correo y `Platform:Enabled`.

Estas observaciones describen diferencias de alcance o información incompleta; no modifican ni reemplazan los documentos anteriores.
