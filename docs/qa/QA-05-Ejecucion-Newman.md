# QA-05 — Casos de prueba y evidencia de Newman

## Caso QA05-01 — Smoke autorizado de autenticación

| Campo | Detalle |
|---|---|
| Objetivo | Comprobar que Newman ejecuta la colección de QA-01 completa y detecta errores de login, autorización o cierre de sesión. |
| Precondiciones | .NET 8, Node.js 20 o compatible, `npm ci` en `tests/identity-negative`; puerto loopback 18502 libre. |
| Datos | Usuario administrador sintético `qa02-admin@test.invalid`; contraseña aleatoria en memoria por ejecución; tokens de vida corta. No se usa una cuenta ni una base compartida. |
| Método | Newman ejecuta en orden `POST /api/auth/login`, `GET /api/auth/administrador`, `POST /api/auth/logout` con `allowAuthentication=true` en un entorno generado en memoria. |
| Resultado esperado | 3 requests emitidos, statuses 200/200/204, 6 aserciones aprobadas, cero fallos; conteos de usuario/operaciones de Plataforma sin cambios. |
| Evidencia | `tests/qa05/results/summary.json`; el job de Actions lo publica como `qa05-newman-evidencia`. |
| Resultado obtenido | 3/3 requests ejecutados: 200, 200 y 204; 6/6 aserciones aprobadas; auditoría sin cambios (`users=3`, `platformWrites=0`). |
| Estado | PASS — ejecutado localmente el 27/10/2026; ver `tests/qa05/results/summary.json`. |

## Caso QA05-02 — Credencial incorrecta produce fallo Newman

| Campo | Detalle |
|---|---|
| Objetivo | Asegurar que una respuesta incorrecta hace fallar Newman y el runner reconoce el fallo. |
| Precondiciones | Host aislado QA05 activo y usuario sintético disponible. |
| Datos | Contraseña aleatoria inválida generada por el fixture; no se persiste. |
| Método | Se ejecuta solo el request de login sobre una copia en memoria del folder smoke. |
| Resultado esperado | El endpoint responde 401, la aserción de HTTP 200 falla y Newman reporta al menos un fallo. El runner valida esa falla esperada; no la considera una corrida positiva del smoke. |
| Evidencia | Campo `negativeCredentialCheck` de `summary.json`, sin contraseña ni cuerpo HTTP. |
| Resultado obtenido | HTTP 401 y 1 fallo reportado por Newman, detectado por el runner como control negativo esperado. |
| Estado | PASS — control de detección ejecutado localmente el 27/10/2026; ver `negativeCredentialCheck` en `tests/qa05/results/summary.json`. |

## Verificación xUnit local

El runner compiló la solución y el host, luego ejecutó `PEI.Identidad.UnitTests`: 197 aprobados, 0 fallidos, 0 omitidos. El TRX local se genera para diagnóstico; el artefacto sanitizado publicado es el resumen JSON.

## Comando reproducible

```sh
cd tests/identity-negative && npm ci
cd ../.. && node tests/identity-negative/run-qa05.mjs
```

El job corre en runner hospedado de GitHub y usa un host local efímero, sin Docker, PostgreSQL, secretos de repositorio o credenciales persistentes. Un status distinto del esperado, requests omitidos, aserciones omitidas/fallidas, errores Newman, fallos xUnit o tests no ejecutados hacen terminar el comando con código distinto de cero.
