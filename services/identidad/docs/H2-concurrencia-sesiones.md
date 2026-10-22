# H2 — Coherencia transaccional de login y refresh

Base: origin/main a8cab06. Trabajo independiente de H1, ID-07, ID-08 e ID-09.
No modifica controladores, HTTP, claims, roles, expiraciones configuradas ni migraciones.

## Garantía y arquitectura

Antes de H2 se emitía el JWT a partir de una lectura sin tracking y después se persistía
el refresh comprobando actividad. Un cambio concurrente de rol o credencial podía separar
la identidad autenticada de la sesión confirmada.

IAuthenticationTokenWriter incorpora dos operaciones transaccionales internas genéricas,
ConfirmarLoginAsync y ConfirmarRefreshAsync. Reciben un callback síncrono que Application
usa únicamente para firmar y construir el resultado desde RefreshIdentity. No debe hacer I/O,
publicar tokens, abrir otras transacciones o introducir efectos externos. Application no recibe
DbContext ni detalles SQL. Infrastructure no decide claims ni firma JWT.

La garantía es identidad coherente al commit, no identidad inmutable después de la respuesta.
Se conservan las comprobaciones de usuario, institución y rol actuales en cada petición JWT.
Los métodos internos anteriores siguen disponibles para compatibilidad y reutilizan las mismas
rutinas transaccionales; logout no cambia. No se elimina código legado como parte de H2.

## Orden de locks

1. Localizar el usuario/institución por IDs; esas primeras consultas no autorizan la emisión.
2. Bloquear Institucion activa con FOR SHARE.
3. Bloquear Usuario activo de esa institución con FOR SHARE.
4. Releer Usuario sin tracking después de ambos locks y exigir rol institucional canónico.
5. En refresh, bloquear el RefreshToken del mismo usuario con FOR UPDATE.

Se conserva este orden en creación y rotación. Las consultas marcadas rotar-refresh/crear-sesion
permiten observar las esperas en las pruebas; no incluyen valores sensibles. Logout solo actualiza
el token y no adquiere posteriormente locks de institución o usuario.
La pertenencia institucional y el propietario del token siguen protegidos por los triggers existentes.
No se ofrece una garantía contra transacciones externas arbitrarias que adquieran locks en otro orden.

En login se autentica primero la contraseña sin mantener locks durante BCrypt. Bajo locks se
comparan PasswordHash, institución y correo con la identidad autenticada. Si cambió la credencial,
se rechaza con el mismo 401 genérico. El rol emitido es el vigente y canónico bajo lock.
Un cambio concurrente de correo también se trata como cambio de credencial y se rechaza.

En refresh se comprueba la vigencia con clock_timestamp() en una sentencia posterior a adquirir
el lock del token. Se emite desde la identidad bloqueada y se vuelve a comprobar el vencimiento
al consumir el token. La CTE de consumo e inserción del reemplazo sigue siendo una sentencia SQL
atómica dentro de la transacción. Si vence durante la firma, no se devuelve el JWT ni se consume
el token anterior. No se cambia la regla de logout del token anterior respecto de sus descendientes.

La firma ocurre antes de persistir y el resultado permanece local hasta CommitAsync. Un fallo
al emitir, insertar o confirmar revierte la operación. Los errores técnicos siguen siendo técnicos;
solo identidad/token no disponibles se traducen a 401. Una pérdida de conexión durante commit
puede dejar resultado indeterminado para el cliente, limitación general de confirmaciones remotas:
no se introduce idempotencia pública ni se promete deshacer un commit ya completado.

## Pruebas y validación

SessionConsistencyTests agrega doce integraciones: cambio de rol en login/refresh, credencial
modificada en login, desactivación de usuario o institución en ambos flujos, fallo JWT en ambos,
fallo de inserción y fallo diferido al commit, y locks de identidad durante la emisión.

Las carreras usan una actualización sin confirmar y observación de pg_stat_activity para demostrar
que la petición espera el lock antes de liberar el cambio. El polling existente observa el estado;
no se usan sleeps para ordenar operaciones. FOR UPDATE NOWAIT verifica los locks durante la firma.
Los triggers de fallos son exclusivos de los esquemas efímeros. Se comprueba que no se publica
un access token en los errores, que no queda consumo parcial y que el token puede reintentarse.

Se preservan las integraciones anteriores de vencimiento durante locks de propietario y token,
refresh simultáneo, carreras refresh/logout, registro, aislamiento y autorización. Se adaptan los
dobles unitarios al puerto ampliado sin eliminar aserciones HTTP ni de seguridad.

PostgreSQL solo se utiliza en un contenedor desechable con datos tmpfs y sin puertos publicados.
El runner comparte su espacio de red; localhost:5541/pei_identidad_tests es ese destino interno,
no la instancia persistente del host. PEI_IDENTIDAD_TEST_CONNECTION es local a ese runner.
Los tests usan esquemas aleatorios y Pooling=false en la conexión externa de esa ejecución.
No se ejecutan semillas o migraciones sobre bases compartidas ni se cambian contenedores existentes.

No hay migraciones nuevas. Hay más consultas cortas por sesión para separar localización,
locks y lectura validada. La firma debe seguir siendo local y breve para limitar el tiempo de locks.
Al integrar ID-07, conservar sus aserciones OpenAPI en InstitutionalSessionTests.cs: H2 solo
adapta el constructor y el doble del escritor. ID-08 e ID-09 no requieren cambios funcionales.

## Resultados

Compilación de PEI.Identidad.sln: 0 errores, 0 advertencias. Unitarias: 112 aprobadas,
0 fallidas y 0 omitidas. Integración PostgreSQL 16 efímero: 60 aprobadas, 0 fallidas y
0 omitidas, incluidas las doce nuevas H2 y las regresiones de concurrencia anteriores.
No se aplicaron migraciones a destinos persistentes. git diff --check correcto.
