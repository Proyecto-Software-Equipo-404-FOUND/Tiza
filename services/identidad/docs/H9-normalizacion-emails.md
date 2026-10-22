# H9 — Normalizacion y unicidad global de emails

## Decision e implementacion

Se admite cualquier proveedor o dominio. No se quitan puntos, sufijos +alias ni caracteres internos. Usuario.Email sigue siendo string, conserva las mayusculas de presentacion y recorta whitespace exterior al asignarse. No se introduce una nueva validacion de sintaxis o de dominio: esa responsabilidad sigue en el registro.

La clave de comparacion es EmailNormalizado, propiedad shadow calculada y almacenada por PostgreSQL: pei_normalize_email(Email). Esta funcion aplica lower(pei_trim_email(valor)); el recorte incluye el mismo conjunto Unicode de espacios que string.Trim() de .NET 8. La conversion de mayusculas es la de la colacion PostgreSQL de la base: se usa la misma funcion en el calculo y en el parametro de login, sin aplicar ToLower de .NET. No se agrega dependencia de extensiones citext o ICU. Si cambia la colacion o su version, revisar y reconstruir claves/indices conforme a las reglas de PostgreSQL.

El indice IX_Usuario_EmailNormalizado es UNIQUE global, sin InstitucionId, sin filtro por Activo y sin depender de filtros EF. Dos cuentas de distintas instituciones, incluso desactivadas, no pueden compartir clave. El indice decide tambien ante carreras concurrentes, actualizaciones masivas y SQL directo. Un trigger separado recorta Email en INSERT y UPDATE de esa columna; no reemplaza los triggers previos de invariantes. Las protecciones de pertenencia, tokens y xmin se conservan.

BuscarUsuarioParaLoginAsync conserva firma y retorno, consulta la clave normalizada mediante la funcion mapeada en EF y sigue exigiendo usuario e institucion activos. El acceso privilegiado anterior al JWT permanece limitado al contrato de autenticacion existente.

## Migracion 20261020143631_NormalizeGlobalEmails

1. Bloquea Usuario para impedir escrituras entre el control de colisiones y la instalacion del indice.
2. Detecta grupos por la futura clave normalizada. Si existen duplicados, lanza SQLSTATE 23505 con nombre de restriccion IX_Usuario_EmailNormalizado y aborta antes de modificar datos. No imprime emails ni IDs en el error.
3. Crea funciones de normalizacion y trigger de recorte. Solo cuando no hay colisiones recorta emails existentes; no altera su capitalizacion, propietario ni identidad.
4. Reemplaza el indice exacto por columna generada e indice unico global.

El Up debe ejecutarse en la transaccion normal de EF, sin suppressTransaction. No extraer statements para ejecutarlos parcialmente ni quitar el bloqueo. La migracion requiere ventana de despliegue: el lock y el calculo de columna/indice pueden demorar y el recorte cambia xmin de las filas afectadas. Se genero con EF Core 8.0.4, junto con designer y snapshot; no se ejecuto contra desarrollo ni otra base.

Down quita exclusivamente las nuevas funciones, trigger y columna, y restaura el indice exacto. No reconstruye los espacios originales eliminados. No afecta triggers de seguridad previos. Aplicar la migracion antes de iniciar la version que usa EmailNormalizado. Usar Migrate/migraciones; EnsureCreated no instala las funciones y triggers personalizados.

## Colisiones existentes

H9-preflight-email-collisions.sql es una consulta de solo lectura, ejecutable antes del despliegue contra el esquema objetivo por su responsable. Devuelve grupos con IDs de usuarios e instituciones; no modifica datos ni expone emails en el resultado. Un resultado vacio significa que no se encontraron colisiones en esa lectura. La migracion vuelve a comprobar bajo bloqueo, para cubrir cambios posteriores.

No se realizo verificacion de datos reales en esta sesion: PEI_IDENTIDAD_TEST_CONNECTION no esta configurada (Process/User/Machine) y no se uso otra conexion. No se afirma que las bases existentes esten libres de colisiones. Si hay grupos conflictivos, detener el despliegue y resolverlos mediante decision humana; nunca fusionar cuentas, renombrarlas o eliminarlas automaticamente.

## Integracion con Ruben

- Se conservan Usuario.Email e IAuthenticationDataAccess; no hay endpoints ni JWT nuevos.
- El registro puede seguir asignando Usuario.Email y guardando con el contexto permitido; no debe asignar EmailNormalizado. La columna generada no necesita un nuevo campo de DTO ni calculo de hash/case en Application o Domain.
- Ante DbUpdateException con PostgresException SQLSTATE 23505 y ConstraintName IX_Usuario_EmailNormalizado, tratar duplicado como error funcional controlado. El nombre anterior IX_Usuario_Email deja de existir: revisar cualquier comparacion por nombre en el registro futuro. No devolver mensajes SQL ni revelar la institucion propietaria.
- Una comprobacion previa de disponibilidad no reemplaza la restriccion unica. Para login usar BuscarUsuarioParaLoginAsync; no hacer igualdad exacta contra Email ni replicar la normalizacion con otra cultura.
- El resto del ciclo de vida de refresh tokens no cambia.

## Pruebas y ejecucion

Las unitarias cubren recorte, proveedores, alias, metadatos del indice, traduccion SQL del parametro y orden del control de colisiones. Las nuevas integraciones usan el fixture existente: verifican destino real pei_identidad_tests local:5541, crean esquemas propios con historial propio, migran solo esos esquemas y eliminan solo sus datos. Cubren registro/login con variantes, duplicados intra/interinstitucion, SQL directo, ExecuteUpdate, concurrencia, inactividad, migracion con y sin colisiones. Los downgrades usados para sembrar datos legacy ocurren unicamente dentro del esquema efimero del caso.

Configurar PEI_IDENTIDAD_TEST_CONNECTION con credenciales exclusivas de esa base fuera del repositorio y ejecutar dotnet test PEI.sln. Sin conexion, las integraciones se reportan omitidas. No usar conexiones de desarrollo ni produccion. Antes de cerrar H9 sigue siendo obligatoria la ejecucion real de PostgreSQL y el preflight sobre los datos que se desplegaran.

Referencias tecnicas: [funciones mapeadas en EF Core](https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping) y [columnas generadas en PostgreSQL 16](https://www.postgresql.org/docs/16/ddl-generated-columns.html).
