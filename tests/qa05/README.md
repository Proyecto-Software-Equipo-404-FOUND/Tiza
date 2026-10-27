# QA-05 — Newman en integración continua

Este flujo ejecuta la carpeta `0. Smoke de autenticación automática (opt-in; crea y revoca sesión)` de la colección aprobada en QA-01. Newman está fijado a `6.2.1` en `tests/identity-negative/package-lock.json`; la instalación reproducible se hace con `npm ci`.

## Ejecución local

Desde la raíz del repositorio, con .NET 8, Node.js y npm instalados:

```powershell
Push-Location tests/identity-negative
npm ci
Pop-Location
node tests/identity-negative/run-qa05.mjs
```

En Linux y macOS se usan los mismos comandos (con `cd` en lugar de `Push-Location`). El script compila Identidad y su host de pruebas, ejecuta xUnit, inicia el host aislado en `127.0.0.1:18502` y corre Newman. Si ese puerto ya está ocupado, falla sin terminar procesos ajenos.

El host reutiliza `Qa02Fixture` y TestServer para ejecutar los endpoints reales con dobles en memoria. Genera un administrador y contraseña aleatorios por proceso, toma esas variables por un canal privado entre procesos y nunca las escribe en consola ni en el artefacto. El smoke realiza `POST /api/auth/login` (200), `GET /api/auth/administrador` (200) y `POST /api/auth/logout` (204). El auditor compara los conteos antes y después.

El job `.github/workflows/qa05-newman.yml` corre en pull requests a `main` y manualmente mediante `workflow_dispatch`; no depende de Docker Desktop ni de servicios persistentes. La colección conserva `allowAuthentication=false` por defecto. Solo el runner aislado habilita el smoke y usa datos sintéticos temporales.

## Criterios de aprobación y evidencia

El proceso devuelve código distinto de cero si falta un request, un status, una aserción, hay un fallo Newman o xUnit reporta fallo u omisión. Como control negativo, clona en memoria la colección y ejecuta solo el login con contraseña aleatoria incorrecta: debe responder 401 y Newman debe reportar el fallo de la aserción. Esto verifica que el runner detecta un contrato incumplido.

El resumen `tests/qa05/results/summary.json` contiene commit, resultados xUnit, método/status y nombres/estado de las aserciones, conteos del auditor y resultado del control negativo. No guarda credenciales, tokens, cuerpos de respuesta ni trazas Newman crudas. GitHub Actions publica el mismo resumen como artefacto incluso ante fallo.

El fixture usa TestServer y dobles en memoria; esta prueba valida integración de la colección con los endpoints/handlers de Identidad, pero no es una prueba contra PostgreSQL.
