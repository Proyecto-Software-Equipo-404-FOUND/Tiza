# QA-04 — aislamiento HTTP real

Requisitos: Docker con motor Linux, .NET SDK8, herramienta `dotnet-ef`8.0.30, Node20/npm. Puertos loopback5541 (PostgreSQL),18541 (Identidad) y18542 (Cursos) libres. No requiere claves ni `.env` del proyecto. Ejecutar desde la raíz:

```sh
dotnet tool install --global dotnet-ef --version 8.0.30
cd tests/tenant-isolation
npm ci
cd ../..
node tests/tenant-isolation/run.mjs
```

Si dotnet-ef ya está instalado, omitir instalación. En Windows, abrir la Terminal Tiza o agregar Docker y `$HOME/.dotnet/tools` al PATH. Opcionalmente `DOCKER_EXE` puede ser la ruta completa al ejecutable de Docker. El runner no acepta conexiones externas ni endpoints arbitrarios.

El runner compila la solución, crea un contenedor PostgreSQL16 con nombre y etiqueta UUID, enlazado sólo a127.0.0.1:5541, aplica migraciones y usa **los seeders existentes ID08/CU06**. Identidad usa `pei_identidad_tests/qa04_seed`; Cursos usa `pei_cursos_qa04`. Ambas bases viven exclusivamente en el nuevo contenedor. Las contraseñas y clave JWT se generan en memoria por ejecución. Los JWT positivos vienen de `/api/auth/login` con Docente A y B. Los tokens firmados artificiales sólo prueban tenant ausente/inválido; uno adicional tiene firma incorrecta.

Dos instituciones, ocho usuarios, cuatro cursos (uno inactivo), dos cohortes y cuatro matrículas del catálogo determinista. La espera de readiness exige cuatro cursos en la base, evitando un falso PASS por base vacía. Se comprueban IDs y tenant de los resultados propios y atributos de B después de ataques PUT/DELETE/POST.

La colección [PEI-QA04-Aislamiento](../postman/PEI-QA04-Aislamiento.postman_collection.json) contiene 39 solicitudes. Su fuente editable es `generate-collection.mjs`; regenerar con `npm run collection` dentro de esta carpeta. CI comprueba que la colección generada coincida con la versionada. Newman falla ante cualquier aserción/error de transporte/script. Las colecciones previas se cargan con Postman SDK y se comprueba su estructura básica; **no se ejecutan completas**, porque contienen flujos distintos. Se conservan sin cambios.

QA04-09 fija resultados antes de ejecutar: listar sin tenant válido 200+vacío, detalle 404, crear 400 ProblemDetails. Son expectativas derivadas de la intención documentada del filtro EF y `CrearCursoCommandHandler`/`DomainException`, no tolerancias para aceptar cualquier respuesta. La ejecución inicial descubrió que EF evaluaba Nullable.Value anticipadamente (409) y un contexto nulo causaba NullReferenceException: se corrigió el filtro, conservando la expectativa. También se corrigió POST cohorte bajo curso ajeno de 400 a 404 según el OpenAPI congelado. La evidencia anterior a la corrección se conserva. QA04-03 fija 404 (padre ajeno), no 200 como alternativa. QA04-10 prueba Docente autorizado y se agrega comprobación real de rol en Identidad (Docente 200, Administrador 403). No certifica permisos de escritura de todos los roles de Cursos: hallazgo QA06-H02.

Después de Newman se ejecutan las suites existentes de Cursos unitarias, Identidad unitarias y **toda Identidad integración con PostgreSQL**. Cada prueba de integración crea su esquema con el mecanismo existente. No se acepta que la suite pase omitiendo pruebas por conexión ausente.

`results/summary.json` y `requests.md` son la evidencia sanitizada: escenario, rol, tenant, endpoint, esperado/obtenido, aserciones y PASS/FAIL. No exportar el summary interno de Newman ni su environment: contienen tokens. Los TRX quedan locales para diagnóstico y no se publican en CI. El workflow sólo sube los dos archivos sanitizados. La limpieza verifica la etiqueta UUID antes de eliminar exclusivamente el contenedor propio y su volumen anónimo; conserva contenedores/volúmenes de desarrollo. Ante terminación forzada del sistema, un administrador puede identificar el contenedor residual por prefijo `tiza-qa04-` y etiqueta `tiza.qa04.run`; nunca ejecutar un prune global.

La revisión contractual está en la rama `feature/QA-06-revision-contratos`, documento `docs/qa/QA06_REVISION_CONTRATOS.md`. Ambas ramas parten de la misma base y son integrables independientemente; el informe consolidado acompaña QA-04.
