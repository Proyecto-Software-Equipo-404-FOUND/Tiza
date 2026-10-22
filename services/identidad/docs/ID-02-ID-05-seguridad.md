# Contratos de seguridad y validacion de ID-02 / ID-05

## Migracion pendiente

ProtectIdentityInvariants agrega triggers sin reescribir ni borrar datos existentes. No fue aplicada a ninguna base. Al desplegarla, PostgreSQL rechazara cambios de Usuario.InstitucionId y de propietario, hash o vencimiento de RefreshToken, y reactivaciones de tokens revocados. Incluye ExecuteUpdate y SQL directo. La migracion toma bloqueos de DDL: planificar su aplicacion fuera de transacciones largas. El rollback elimina solamente estos triggers y su funcion, y retira la proteccion.

Los filtros EF no son una frontera absoluta. IgnoreQueryFilters permite lecturas globales; SQL directo y operaciones masivas pueden modificar otros atributos o eliminar registros de otros tenants si se usan sin condicion. Los triggers protegen las invariantes, NO implementan autorizacion por fila. La cuenta de runtime no debe ser propietaria ni tener permisos de DDL o deshabilitar triggers. No se agrego RLS: requiere decidir junto con autenticacion como establecer la identidad transaccional en conexiones reutilizadas y como separar el acceso privilegiado previo al JWT. Tampoco se modifica BuildingBlocks, para evitar afectar otros servicios.

## Integracion con Ruben

Se conservan los metodos existentes y se agregan metodos opcionales de consulta de identidad y rotacion. No se agregan endpoints ni JWT. BuscarIdentidadParaRenovacionAsync proyecta UsuarioId, InstitucionId, Email y Rol sin PasswordHash; los metodos antiguos devuelven entidades internas y no deben serializarse en HTTP.

RotarRefreshTokenAsync consume el anterior y agrega el reemplazo en un unico statement atomico. false indica que no se emitio reemplazo. Solo emitir JWT y entregar el token nuevo si devuelve true y la transaccion externa, si existe, confirma. Ante excepcion no emitir credenciales. Un hash duplicado hace fallar todo el statement, conservando el anterior. La consulta previa no constituye autorizacion suficiente para emitir: la rotacion revalida usuario e institucion activos y toma bloqueos compartidos durante el statement. Una desactivacion posterior es otro evento de seguridad: su flujo debe revocar todas las sesiones pertinentes. RevocarRefreshTokenAsync permite revocar aun si la cuenta esta inactiva o el token vencio, y no rehabilita tokens.

Los hashes deben ser no vacios, sin espacios y de hasta 255 caracteres; fechas UTC futuras. La eleccion del algoritmo, codificacion, entropia, duracion y deteccion de reutilizacion por familia requiere acuerdo con Ruben. No se impuso unilateralmente un algoritmo. No transmitir hashes ni PasswordHash a clientes. Los registros y errores no deben incluir tokens.

TenantContext exige identidad autenticada y un solo institucion_id no vacio. El middleware JWT pendiente debe rechazar claims invalidos con 401 y validar firma, emisor, audiencia y vencimiento antes de acceder a datos. El tenant invalido cierra consultas ordinarias y escrituras. El manejador API traduce TenantAccessDeniedException a 403 generico. Las politicas de rol y usuario siguen siendo responsabilidad de la capa de autorizacion. La interfaz local ITenantContext se mantiene separada de BuildingBlocks de forma deliberada hasta acordar un adaptador; no registrar por accidente la interfaz compartida.

## Email: decision aprobada e implementacion H9

Se conserva unicidad global y se compara sin mayusculas ni espacios exteriores. La migracion NormalizeGlobalEmails agrega una clave calculada e indice unico, y aborta si encuentra colisiones. No fue aplicada a desarrollo. Consultar H9-normalizacion-emails.md y H9-preflight-email-collisions.sql para contratos, verificacion previa y validaciones pendientes.

## Pruebas

Configurar PEI_IDENTIDAD_TEST_CONNECTION fuera del repositorio con credenciales exclusivas de testing. Solo se acepta localhost/127.0.0.1/::1, puerto 5541 y base pei_identidad_tests, y se confirma current_database() antes de escribir. No copiar credenciales de produccion. El rol de pruebas necesita CREATE sobre esa base; no usar ese rol en desarrollo o produccion.

Cada caso crea un esquema pei_test_<GUID>, con historial de migraciones propio, aplica migraciones alli y crea datos aleatorios. Al terminar elimina exclusivamente ese esquema. No se borran ni recrean bases y no se modifican datos precargados. Si se interrumpe el proceso pueden quedar esquemas; su limpieza posterior requiere verificar propietario y ejecucion de origen, nunca borrado global por prefijo. Sin conexion las pruebas de integracion se reportan omitidas, no aprobadas.

Ejecutar desde la raiz: dotnet build PEI.sln; dotnet test PEI.sln. La solucion raiz incluye ahora integracion. No ejecutar integracion con una conexion de desarrollo. La validacion local sin conexion no confirma el SQL de triggers, locks o rotacion: esa ejecucion real queda requerida antes de aprobar.

Se mapea xmin como token de concurrencia optimista en las tres entidades y se actualiza el snapshot; xmin ya existe como columna de sistema PostgreSQL y no requiere ALTER TABLE. Una escritura rastreada obsoleta produce DbUpdateConcurrencyException; su consumidor debe responder con conflicto y recargar, sin reintento ciego. Las operaciones masivas siguen requiriendo condiciones explicitas si quieren detectar concurrencia sobre otros atributos.
