# QA-02 — Identidad: negativos, permisos y errores

Requisitos de la suite principal: .NET8 y Node20. No requiere Docker, base de datos, credenciales externas ni servidores ya iniciados. Desde la raíz:

```sh
cd tests/identity-negative
npm ci
cd ../..
node tests/identity-negative/run.mjs
```

El runner compila `services/identidad/PEI.Identidad.sln`, compila el host de QA y ejecuta todos los tests unitarios existentes y nuevos. Inicia después un puente Kestrel exclusivamente en `127.0.0.1:18502`, que reenvía las solicitudes a `TestServer` de la API real. Se reutilizan `PlatformHttpFactory` y `FakeIdentityStore`; sólo se reemplaza adicionalmente `IRegistrationDataAccess` por ese mismo store en memoria. Se mantienen los validadores, controladores, handlers, JWT, políticas y middleware productivos. La conexión de la fábrica apunta al destino inaccesible `not_used`; no se modifican configuraciones productivas.

`Qa02Fixture` crea tres usuarios sintéticos (Administrador, Docente, Alumno) y contraseñas/claves/tokens efímeros. El protocolo de arranque transmite variables por stdout privado al proceso padre. **No ejecutar el DLL del host redirigiendo stdout a terminal, archivos o CI**: contiene los tokens de este fixture. El runner los consume en memoria y nunca publica ese canal. El host no se incluye en las soluciones/productos publicados y no agrega rutas a la API productiva. Su ruta local de auditoría sólo devuelve contadores; verifica que los casos inválidos no creen usuarios ni instituciones.

La carpeta `QA-02 — Casos negativos y permisos de Identidad` se añade a la colección existente. Las carpetas anteriores, auth, scripts y variables permanecen idénticos (comprobación semántica automatizada contra la base853f3f6). El environment de ejemplo no se modifica: `allowMutations=false` sigue siendo el valor por defecto. La carpeta nueva omite todas sus peticiones si falta `qa02Fixture=isolated-memory-v1`; sus POST además requieren `allowMutations=true`. El runner verifica la guarda antes de habilitar la ejecución efímera. Estas variables son una barrera operativa, no autenticación: no habilitarlas sobre una base persistente. No ejecutar toda la colección; el runner selecciona únicamente QA-02.

`generate.mjs` es la fuente de los33 casos, genera `cases.json` y actualiza únicamente esa carpeta de Postman. Los17 casos de400 se reutilizan desde xUnit para comprobar errores y ausencia de persistencia, sin duplicar las pruebas JWT ya existentes. Para actualizar casos:

```sh
node tests/identity-negative/generate.mjs
```

El runner guarda sólo `results/summary.json`: casos, esperado/obtenido, Content-Type, nombres y resultados de aserciones, contadores de persistencia y totales .NET. No guarda bodies, headers Authorization, variables o reportes crudos de Newman. Los TRX quedan locales para diagnóstico y no se publican en CI. El workflow `qa02-identidad.yml` ejecuta realmente Newman y xUnit; no modifica `ci.yml` ni los workflows QA-04/QA-06. QA-05 no tenía rama/PR encontrado al inspeccionar: este workflow se limita al fixture QA-02 y puede reutilizarse posteriormente desde una orquestación general.

Regresión adicional opcional de la suite existente con PostgreSQL:

```sh
node tests/identity-negative/postgres.mjs
```

Requiere Docker Linux y puerto127.0.0.1:5541 libre. Crea `tiza-qa02-<uuid>`, etiqueta de ejecución y base `pei_identidad_tests`; cada test existente crea su esquema. Verifica etiqueta antes de eliminar exclusivamente su contenedor y volumen. No acepta conexiones externas ni utiliza datos de desarrollo. Produce `postgres-summary.json`; la CI principal QA-02 **no ejecuta este script**. Si se interrumpe forzosamente el proceso, identificar sólo el contenedor propio por nombre/etiqueta; no hacer prune global.

Las validaciones exigen el código y Content-Type efectivo. No requieren `type/detail/instance` cuando el contrato los omite. Para errores de binding/DTO exigen `errors` no vacío. Un JWT sin rol se rechaza como identidad inválida401, mientras un rol canónico válido sin permiso da403. Los errores de Plataforma conservan `application/json`, según ID07. Un logout vacío devuelve204, no se inventa una validación400.
