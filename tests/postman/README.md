# Postman — colección de Identidad de Tiza

En `main` ya están versionados la colección ID-07 y su entorno de ejemplo. Esta carpeta añade un smoke test explícito para comprobar el inicio de sesión automático contra una API real.

## Importar y ejecutar el smoke test

1. Iniciar PostgreSQL de Identidad y la API siguiendo `infra/README.md` y `services/identidad/docs/ID07-SWAGGER-POSTMAN.md`. El perfil HTTP real de Identidad escucha en `http://localhost:5293`; la ruta de Swagger en Development es `/swagger`.
2. En Postman, importar `identidad.postman_collection.json` y `identidad.local.postman_environment.json`; seleccionar el entorno `Tiza Identidad — ejemplo local sin secretos`.
3. Obtener una cuenta **Administrador institucional de pruebas que ya exista** en el entorno local. Completar `institutionalEmail` y guardar su contraseña en el valor local/secret de `institutionalPassword`. El ejemplo no incluye una contraseña. No registrar una cuenta, activar Plataforma, ni aplicar migraciones para preparar esta colección.
4. Establecer `allowAuthentication` en `true` solo para la ejecución elegida. Abrir la carpeta `0. Smoke de autenticación automática (opt-in; crea y revoca sesión)` y ejecutarla completa, en orden.
5. El primer request llama `POST {{baseUrl}}/api/auth/login` con `{ "email": "{{institutionalEmail}}", "password": "{{institutionalPassword}}" }`. Valida HTTP 200 y los campos reales `accessToken`, `refreshToken`, `tokenType`, `accessTokenExpiresAt` y `refreshTokenExpiresAt`; guarda los dos tokens en variables secretas del entorno.
6. El segundo request llama `GET {{baseUrl}}/api/auth/administrador` con `Authorization: Bearer {{institutionalToken}}`, tomado automáticamente del login. Valida HTTP 200 y `mensaje` de tipo string.
7. El tercer request envía el `refreshToken` a `POST /api/auth/logout`, valida HTTP 204 sin cuerpo y revoca la sesión creada.
8. Volver `allowAuthentication` a `false` y limpiar el email/contraseña y tokens locales cuando termines. Los flags `type: secret` en un JSON importado **no** convierten el archivo en un almacén seguro; el ejemplo versionado guarda esos valores vacíos.

La carpeta se omite por defecto. Ejecutarla crea y revoca una sesión real, aunque el request protegido solo lee datos. El resto de la colección mantiene sus guardas existentes: `allowMutations=false` evita por defecto refresh, logout, registro y operaciones mutables; los casos de roles distintos requieren tokens válidos separados si se ejecutan.

## Archivos y otros flujos

- `identidad.postman_collection.json`: colección Postman v2.1 para rutas reales del controlador de Identidad; incluye sesiones, roles, casos negativos y Plataforma (si se habilitó expresamente).
- `identidad.local.postman_environment.json`: `baseUrl=http://localhost:5293`, flags apagados, valores de tokens/contraseñas vacíos y correos `.test` de ejemplo.
- `../cursos/` no existe en esta carpeta; la colección de Cursos está en `PEI-Cursos.postman_collection.json` y tiene requisitos propios. Esta guía describe el flujo de Identidad.
- Referencia de contratos, respuestas, configuración sensible y uso seguro: `services/identidad/docs/ID07-SWAGGER-POSTMAN.md`.

No importes valores reales ni exportes el entorno después de escribir secretos. Para la colección completa, usa una base efímera autorizada y revisa cada carpeta antes de permitir mutaciones; no ejecutes pruebas de escritura en una base compartida.

## Problemas comunes

- **401 en login:** comprobar que la cuenta está activa, pertenece a la institución configurada y que la contraseña corresponde al ambiente local.
- **403 en el GET protegido:** el usuario autenticado no tiene rol Administrador. La carpeta no convierte otros roles en administrador.
- **Carpeta omitida:** `allowAuthentication` debe ser exactamente `true` para correr el flujo opt-in.
- **Connection refused:** iniciar la API y revisar `baseUrl`; no agregar una barra al final.
- **409 en registro de otras carpetas:** el correo sintético ya fue usado; la colección no borra ni reinicia datos automáticamente.
- **429:** la API limita logins por IP; esperar la ventana indicada por la configuración local antes de reintentar.
