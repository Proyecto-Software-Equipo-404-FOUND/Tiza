# Contrato del Hub SignalR — Sala en Vivo v1

| Campo | Valor |
| --- | --- |
| **Tarea** | SA-05 — Contrato de Hub SignalR y envío idempotente |
| **Servicio** | `PEI.SalaEnVivo` |
| **Contrato** | `sala-hub-v1` |
| **Ruta del hub** | `/hubs/sala` |
| **Transporte** | WebSockets; fallback Server-Sent Events / Long Polling |
| **Autenticación** | JWT Bearer mediante `access_token` en query string |
| **Estado** | Congelado para Sprint 1. Cambios aditivos dentro de v1; un cambio rupturista requiere v2 |
| **Implementación** | SA-04 (unión), SA-06 (eventos), S2-SA-01 (respuestas idempotentes), S2-SA-02 (reconexión) |
| **Revisores** | Evaluaciones, Frontend 2, QA |

> Este documento es la fuente de verdad del contrato de tiempo real entre el frontend y
> `PEI.SalaEnVivo`. Los esquemas machine-readable viven en
> `contracts/schemas/enviar-respuesta-sala-v1.schema.json` y
> `contracts/schemas/resultado-envio-respuesta-sala-v1.schema.json`.
> Los ejemplos ejecutables viven en `contracts/signalr/ejemplos/`.

---

## 1. Objetivo y alcance

Definir, antes de implementar, el contrato de comunicación del Hub de Sala en Vivo:

- los **métodos** que el cliente puede invocar;
- los **eventos** que el servidor emite;
- el **payload de envío de respuestas** (`preguntaId`, `respuesta`, `idempotencyKey`,
  `secuencia`);
- las reglas de **idempotencia y orden**;
- el **catálogo de errores** y la política de reintentos.

**Dentro del alcance:** contrato de red, semántica de idempotencia, validaciones y
errores observables por el cliente.

**Fuera del alcance:** endpoints HTTP (`POST /api/v1/salas`), esquema físico de la base,
workers de cierre, outbox hacia Evaluaciones y detalles de la corrección con IA. Esos
puntos se documentan en sus propias tareas (SA-03, S2-SA-01, S3-SA-01).

Este contrato se escribe **contract-first**: primero se congela acá, después se implementa
en SA-06 y S2-SA-01, y las pruebas de Postman/Cypress toman los ejemplos de este
directorio. Si la implementación necesita cambiar el contrato, primero se cambia el
documento y los esquemas en el mismo PR.

---

## 2. Conexión y autenticación

### 2.1 URLs

| Entorno | URL del hub | Observaciones |
| --- | --- | --- |
| Desarrollo directo | `http://localhost:5104/hubs/sala` | Perfil local de `PEI.SalaEnVivo.Api` |
| Gateway YARP | `http://localhost:8080/hubs/sala` | Ruta `sala-hub-route` de `gateway/src/PEI.Gateway/appsettings.json` |
| Producción | `https://<host>/hubs/sala` | Solo `wss`/HTTPS; nunca texto plano |

El cliente del frontend ya usa `HubConnectionBuilder.withUrl(`${VITE_API_URL}/hubs/sala`,
{ accessTokenFactory })` en `frontend/src/shared/realtime/conexion.ts`. El gateway debe
hacer passthrough de WebSockets y no debe registrar el query string completo.

### 2.2 Token JWT

- Se envía como `access_token` en el query string del handshake (SignalR no permite
  headers personalizados en WebSockets desde el navegador).
- `Program.cs` ya lee `context.Request.Query["access_token"]` para rutas que empiezan con
  `/hubs/sala`, y valida `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SecretKey` y `Lifetime`.
- Claims requeridos:

| Claim | Uso | Ejemplo |
| --- | --- | --- |
| `sub` o `ClaimTypes.NameIdentifier` | Identifica al alumno/docente | `22222222-2222-4222-8222-222222222222` |
| `institucion_id` | Aísla el tenant; se compara con `Sala.InstitucionId` | `11111111-1111-4111-8111-111111111111` |
| `role` | `Alumno` o `Docente` | `Alumno` |

> **Brecha detectada:** `JwtTokenService` de Identidad emite el claim
> `institucion_id`, pero `SalaHub` hoy busca `InstitucionId`. Con el token real, el hub
> devolvería “Institución no válida”. Al implementar SA-04/SA-06 hay que leer
> `institucion_id` (y, si se quiere compatibilidad, aceptar también `InstitucionId`).
> Este documento usa `institucion_id` como nombre canónico.

El servidor **nunca** toma `alumnoId` ni `institucionId` de un payload del cliente: los
deriva del `Context.User`. `institucion_id` es obligatorio; si falta, la conexión se
rechaza.

### 2.3 Handshake y fallos de conexión

| Situación | Resultado observable | Acción del cliente |
| --- | --- | --- |
| Sin token, token vencido o firma inválida | Handshake HTTP `401` | Refrescar sesión y reconectar; no reintentar en bucle |
| Token válido sin rol permitido | Handshake HTTP `403` | Mostrar “no autorizado”; no reintentar |
| Ruta incorrecta | HTTP `404` | Corregir URL/base del gateway |
| Límite de conexiones (si se configura) | HTTP `429` | Backoff exponencial |
| Corte de red | Evento `onreconnecting` / `onclose` | `withAutomaticReconnect` y botón “Reintentar” |

El token **no** debe aparecer en logs, trazas, URLs de analítica ni mensajes de error.
Para el gateway, la ruta debe aceptar exactamente `/hubs/sala` (sin barra final
obligatoria); SA-01 valida ese caso.

### 2.4 Reconexión

`withAutomaticReconnect()` reintenta con los intervalos por defecto (0, 2, 10 y 30
segundos). Al reconectar:

1. El cliente vuelve a invocar `UnirseASala(pin)` (debe ser idempotente para un alumno ya
   registrado) o `ObtenerEstadoSala()` si conserva el `salaId`.
2. El servidor responde con el evento `estadoSala` al llamador.
3. El cliente restaura `ultimaSecuencia`, la pregunta vigente y el tiempo restante.
4. Cualquier respuesta pendiente se reenvía con la **misma** `idempotencyKey` y
   `secuencia`; nunca con una clave nueva.

La reconexión completa (recuperar pregunta y respuestas) es S2-SA-02. En v1 se documenta
el contrato; la implementación puede llegar después.

---

## 3. Convenciones del contrato

- **Métodos:** PascalCase (`UnirseASala`, `EnviarRespuesta`). SignalR los expone con ese
  nombre.
- **Eventos:** camelCase (`alumnoIngreso`, `preguntaActual`). El cliente debe registrar
  exactamente ese nombre con `hub.on(...)`.
- **JSON:** camelCase, UTF-8, sin envoltorios adicionales.
- **UUID:** string con formato UUID v4.
- **Fechas:** ISO 8601 UTC con sufijo `Z` (ej. `2026-10-26T14:03:11.482Z`). La hora del
  servidor es la autoritativa; el cliente solo la usa para mostrar.
- **Enums:** PascalCase (`Aceptada`, `OpcionMultiple`, `Docente`).
- **Un argumento por método:** los métodos con varios campos reciben un único objeto
  payload (`EnviarRespuesta(payload)`); los que tienen un solo valor simple conservan el
  escalar (`pin`, `salaId`, `motivo`, `preguntaId`). Esto permite agregar campos a los
  payloads sin cambiar la firma.
- **`invoke` vs `send`:** el envío de respuestas usa `invoke` porque necesita el acuse.
  `send` (fire-and-forget) está prohibido para respuestas.
- **Compatibilidad:** clientes y servidor ignoran campos desconocidos. Campos nuevos son
  opcionales; quitar o cambiar el tipo de un campo exige v2.
- **Privacidad:** a los alumnos nunca se les envía `esCorrecta`, `rubrica` ni datos
  personales de otros alumnos. Los eventos usan alias, no nombres ni correos.
- **Tamaño:** `respuesta` de preguntas abiertas: 1 a 10 000 caracteres. El servidor
  recorta espacios al inicio y al final antes de validar y persistir.

## 4. Roles, grupos y autorización

### 4.1 Grupos

| Grupo | Miembros | Eventos que recibe |
| --- | --- | --- |
| `{salaId}` | Alumnos y docentes unidos a la sala | `salaIniciada`, `preguntaActual`, `salaFinalizada` |
| `{salaId}:docentes` | Solo docentes de la sala | `alumnoIngreso`, `respuestaRegistrada` |

SA-04 ya agrega al alumno usando `salaId.ToString()` como nombre de grupo. El contrato
mantiene ese nombre para el grupo de sala (`{salaId}`) y reserva `{salaId}:docentes` para
SA-06. No cambiar el nombre sin migrar a todos los clientes.

### 4.2 Matriz de autorización

| Método | Alumno | Docente | Administrador |
| --- | --- | --- | --- |
| `UnirseASala(pin)` | Sí | No | No |
| `UnirseASalaDocente(salaId)` | No | Sí | No |
| `EnviarRespuesta(payload)` | Sí | No | No |
| `ObtenerEstadoSala()` | Sí | Sí | No |
| `IniciarSala()` | No | Sí | No |
| `AvanzarPregunta(preguntaId?)` | No | Sí | No |
| `FinalizarSala(motivo?)` | No | Sí | No |

El hub usa `[Authorize]`; además, cada método valida el rol y la pertenencia a la sala.
Un alumno no puede enviar respuestas de otro alumno ni de otra institución. El
`InstitucionId` de la sala se compara siempre contra el claim del token.

---

## 5. Métodos del Hub

| Método | Dirección | Rol | Retorno | Tarea |
| --- | --- | --- | --- | --- |
| `UnirseASala(pin)` | Cliente → Servidor | Alumno | `void` en SA-04; objetivo: `ResultadoUnirseASala` | SA-04 / S2-SA-02 |
| `UnirseASalaDocente(salaId)` | Cliente → Servidor | Docente | `void` | SA-06 |
| `EnviarRespuesta(payload)` | Cliente → Servidor | Alumno | `ResultadoEnvioRespuesta` | S2-SA-01 |
| `ObtenerEstadoSala()` | Cliente → Servidor | Alumno/Docente | `EstadoSala` | S2-SA-02 |
| `IniciarSala()` | Cliente → Servidor | Docente | `void` | SA-06 |
| `AvanzarPregunta(preguntaId?)` | Cliente → Servidor | Docente | `void` | SA-06 |
| `FinalizarSala(motivo?)` | Cliente → Servidor | Docente | `void` | SA-06 |

Regla de errores por método:

- **`EnviarRespuesta`** no lanza excepciones de negocio: siempre resuelve con
  `ResultadoEnvioRespuesta` (`Aceptada`, `Duplicada` o `Rechazada`). Así el cliente puede
  reintentar de forma idempotente sin depender del texto de una excepción.
- **Métodos de unión/control** (`UnirseASala`, `UnirseASalaDocente`, `ObtenerEstadoSala`,
  `IniciarSala`, `AvanzarPregunta`, `FinalizarSala`) usan `HubException` con el formato
  **`CODIGO: mensaje legible`**, por ejemplo
  `throw new HubException("SALA_LLENA: la sala alcanzó su capacidad máxima.")`.
  El cliente separa por el primer `:` y usa `CODIGO` para decidir; el texto es solo para
  mostrar.
- **Errores asíncronos** que no pertenecen a una invocación concreta se emiten con el
  evento `error` (ver sección 6.7).
- **Errores de protocolo/autenticación** ocurren antes de ejecutar el método y se
  manifiestan como fallo de handshake o cierre de conexión (sección 2.3).

> **Brecha conocida con SA-04:** la implementación actual de `UnirseASala` devuelve
> `void` y lanza `HubException` con mensajes sin código. El contrato objetivo es un
> resultado estructurado e idempotente para permitir la reconexión. Mientras no se
> ajuste, el cliente debe mapear los mensajes actuales (ver sección 8.4) y tratar
> “El alumno ya está registrado en la sala” como un ingreso exitoso si el `salaId`
> coincide, para no bloquear la reconexión.

### 5.1 `UnirseASala(pin)`

```text
UnirseASala(pin: number) -> void | ResultadoUnirseASala
```

- **Rol:** alumno autenticado.
- **Payload:** `pin` entero de 6 dígitos (100000–999999). Es el PIN generado por
  `POST /api/v1/salas`.
- **Efecto:** valida PIN, estado y vigencia de la sala (con la excepción de reingreso de
  un alumno ya registrado, ver abajo); valida que `Sala.InstitucionId` coincida con el
  claim; registra al alumno si es la primera vez; agrega la conexión al grupo `{salaId}`;
  notifica `alumnoIngreso` a `{salaId}:docentes`; envía `estadoSala` al llamador.
- **Idempotencia:** si el alumno ya está registrado en esa sala, debe responder éxito
  con el mismo `salaId` (reconexión). No debe fallar con “ya registrado”.
- **Sala finalizada:** si el alumno ya estaba registrado, el reingreso es exitoso y
  `estadoSala.estado` es `Finalizada` (para mostrar el cierre o los resultados). Un alumno
  nuevo en una sala finalizada recibe `SALA_FINALIZADA`.
- **Resultado objetivo:**

```json
{
  "estado": "Aceptada",
  "salaId": "11111111-1111-4111-8111-111111111111",
  "recibidaUtc": "2026-10-26T13:59:58.120Z",
  "error": null
}
```

- **Errores:** `SALA_NO_ENCONTRADA`, `SALA_OTRA_INSTITUCION`, `SALA_LLENA`,
  `SALA_FINALIZADA`, `NO_AUTORIZADO`, `ERROR_INTERNO` (sección 8).

### 5.2 `EnviarRespuesta(payload)`

```text
EnviarRespuesta(payload: EnviarRespuestaRequest) -> ResultadoEnvioRespuesta
```

Es el método central de esta tarea. El cliente lo invoca con `invoke` (no `send`) y
espera el acuse antes de habilitar el envío de la siguiente respuesta.

#### 5.2.1 Payload de envío

El payload tiene exactamente estos cuatro campos:

| Campo | Tipo | Obligatorio | Reglas |
| --- | --- | --- | --- |
| `preguntaId` | string UUID | Sí | Debe pertenecer a la evaluación publicada de la sala y ser la pregunta vigente. Inmutable durante la sala. |
| `respuesta` | string | Sí | 1 a 10 000 caracteres. Si la pregunta es `OpcionMultiple`, debe ser el UUID de una opción de esa pregunta. Si es `Abierta`, es el texto de la respuesta. Se recorta antes de validar; no puede quedar vacío. |
| `idempotencyKey` | string UUID | Sí | UUID v4 generado por el cliente **una vez por respuesta lógica**. Se reutiliza solo en reintentos de esa misma respuesta. Ámbito: `(salaId, alumnoId)`. |
| `secuencia` | integer | Sí | Mayor o igual a 1. Contador por `(salaId, alumnoId)`, no por pregunta. El siguiente valor esperado es `ultimaSecuencia + 1`. Se reutiliza en reintentos. |

El payload **no** incluye `salaId`, `alumnoId` ni `institucionId`. El servidor los deriva
de la conexión autenticada y de la sala activa de esa conexión. En v1 se admite una sala
activa por conexión; si en el futuro se necesita multi-sala, se agrega `salaId` en v2.

#### 5.2.2 Ejemplo — opción múltiple

```json
{
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "respuesta": "7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
  "idempotencyKey": "b3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "secuencia": 1
}
```

#### 5.2.3 Ejemplo — respuesta abierta

```json
{
  "preguntaId": "9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f",
  "respuesta": "Una variable es un espacio en memoria con un nombre y un tipo asociado.",
  "idempotencyKey": "c4e2d3f5-6a7b-4c8d-9e0f-1a2b3c4d5e6f",
  "secuencia": 2
}
```

Los archivos equivalentes están en
`contracts/signalr/ejemplos/enviar-respuesta-opcion-multiple.json` y
`contracts/signalr/ejemplos/enviar-respuesta-abierta.json`.

#### 5.2.4 Algoritmo del servidor

El servidor debe ejecutar estas validaciones en orden. El orden importa para que los
errores sean deterministas, para que un reintento perdido siga siendo idempotente y para
no consumir secuencia en un rechazo.

1. **Identidad y sala:** obtener `alumnoId` e `institucionId` del token y la sala unida a
   la conexión (en cualquier estado). Si la conexión no está unida a una sala:
   `ALUMNO_NO_EN_SALA`.
2. **Forma del payload:** validar campos obligatorios, tipos y formato UUID. Si falla:
   `PAYLOAD_INVALIDO`. La longitud y el contenido de `respuesta` se validan en el paso 7.
3. **Idempotencia:** buscar una fila existente por
   `(salaId, alumnoId, idempotencyKey)` **antes** de validar estado, vigencia y tiempo.
   Un reintento de un envío ya aceptado debe responder `Duplicada` aunque la pregunta o
   la sala se hayan cerrado mientras tanto.
   - Si existe y el payload canónico coincide (`preguntaId` + `respuesta` normalizada +
     `secuencia`): devolver `Duplicada` con el `recibidaUtc` original, sin escribir ni
     emitir eventos.
   - Si existe y el payload difiere: `IDEMPOTENCY_KEY_REUTILIZADA`.
4. **Estado de la sala:** debe estar `EnCurso`. Si está `Finalizada`: `SALA_FINALIZADA`.
   Si no comenzó: `SALA_NO_INICIADA`.
5. **Pregunta:** debe existir en la evaluación publicada y ser la pregunta vigente. Si no
   existe: `PREGUNTA_INEXISTENTE`. Si ya se avanzó o se cerró: `PREGUNTA_NO_VIGENTE`.
6. **Tiempo:** si `nowUtc > fechaLimiteUtc` de la pregunta o de la sala:
   `TIEMPO_AGOTADO`.
7. **Tipo de respuesta:** para `OpcionMultiple`, `respuesta` debe ser una opción de la
   pregunta; para `Abierta`, texto no vacío de 1 a 10 000 caracteres. Si no:
   `RESPUESTA_INVALIDA`.
8. **Una respuesta por pregunta:** buscar por `(salaId, alumnoId, preguntaId)`. Si ya
   existe una fila: `RESPUESTA_YA_REGISTRADA`. La primera respuesta válida es la
   definitiva; no se permite sobrescribir.
9. **Secuencia:** si `secuencia != ultimaSecuencia + 1`:
   `SECUENCIA_FUERA_DE_ORDEN`. La única excepción es el caso de duplicado del paso 3.
10. **Persistencia transaccional:** insertar la respuesta y actualizar
    `ultimaSecuencia` en la misma transacción. Los índices únicos de la sección 9.2 son
    la última defensa ante dos envíos concurrentes.
11. **Evento:** emitir `respuestaRegistrada` a `{salaId}:docentes` (sin el texto de la
    respuesta) y devolver `Aceptada`.

Si dos envíos concurrentes pasan las validaciones a la vez, uno gana el `INSERT` y el otro
recibe una violación de índice único. El perdedor debe **releer** y resolver como
`Duplicada` (misma clave) o `RESPUESTA_YA_REGISTRADA` / `SECUENCIA_FUERA_DE_ORDEN`
(claves distintas). Nunca debe devolver un 500 por una carrera esperable.

#### 5.2.5 Resultado del envío

```json
{
  "estado": "Aceptada",
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "idempotencyKey": "b3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "secuencia": 1,
  "recibidaUtc": "2026-10-26T14:03:11.482Z",
  "error": null
}
```

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `estado` | `Aceptada` \| `Duplicada` \| `Rechazada` | Resultado de la operación |
| `preguntaId` | string UUID o `null` | Eco del payload; `null` si no se pudo interpretar |
| `idempotencyKey` | string UUID o `null` | Eco del payload; `null` si no se pudo interpretar |
| `secuencia` | integer o `null` | Eco del payload; `null` si no se pudo interpretar |
| `recibidaUtc` | string date-time | Hora del servidor en que se procesó la invocación. En `Duplicada` es la hora original de la fila persistida, no la del reintento |
| `error` | objeto o `null` | Presente solo si `estado = Rechazada` |

Semántica de los estados:

- **`Aceptada`:** se persistió una fila nueva. La secuencia queda consumida. El cliente
  actualiza `ultimaSecuencia` y puede enviar la siguiente respuesta.
- **`Duplicada`:** ya existía una fila con la misma `idempotencyKey` y el mismo payload
  canónico. No se escribe nada nuevo, no se emite `respuestaRegistrada` y se devuelve el
  `recibidaUtc` original. El cliente lo trata como **éxito** y deja de reintentar.
- **`Rechazada`:** no se persistió nada, no se consumió secuencia y no se emitió evento.
  El objeto `error` explica el motivo. El cliente decide según `reintentable`.

Ejemplo de duplicado (mismo `recibidaUtc` que el original):

```json
{
  "estado": "Duplicada",
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "idempotencyKey": "b3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "secuencia": 1,
  "recibidaUtc": "2026-10-26T14:03:11.482Z",
  "error": null
}
```

Ejemplo de rechazo:

```json
{
  "estado": "Rechazada",
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "idempotencyKey": "b3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "secuencia": 1,
  "recibidaUtc": "2026-10-26T14:03:12.010Z",
  "error": {
    "codigo": "PREGUNTA_NO_VIGENTE",
    "mensaje": "La pregunta ya no está vigente.",
    "reintentable": false,
    "correlationId": "0f7c2f0e-9a1b-4c2d-8e3f-4a5b6c7d8e9f",
    "detalles": {
      "preguntaActualId": "9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f"
    }
  }
}
```

### 5.3 `ObtenerEstadoSala()`

```text
ObtenerEstadoSala() -> EstadoSala
```

- **Rol:** alumno o docente unido a la sala.
- **Uso:** reconexión, recarga de pestaña y sincronización de secuencia.
- **Efecto:** no modifica datos. Devuelve la pregunta vigente, el tiempo restante y el
  estado de las respuestas del alumno.
- **Payload de respuesta:** ver `estadoSala` en la sección 6.5. Si la sala ya terminó,
  devuelve `estado = Finalizada` con `preguntaActual = null`; no es un error, porque el
  cliente necesita pintar el cierre.
- **Errores:** `ALUMNO_NO_EN_SALA`, `SALA_NO_ENCONTRADA`, `NO_AUTORIZADO`, `ERROR_INTERNO`.

### 5.4 Métodos del docente (SA-06)

Estos métodos son parte del contrato, pero su implementación corresponde a SA-06. Se
documentan para que Frontend 2 y QA puedan construir el panel y las pruebas desde ahora.

| Método | Payload | Efecto | Errores |
| --- | --- | --- | --- |
| `UnirseASalaDocente(salaId)` | `{ salaId: UUID }` o UUID posicional | Valida que el docente pertenezca a la institución de la sala; agrega la conexión a `{salaId}:docentes`; envía `estadoSala` al llamador | `NO_AUTORIZADO`, `SALA_NO_ENCONTRADA`, `SALA_FINALIZADA` |
| `IniciarSala()` | ninguno | Pasa la sala a `EnCurso`, fija `iniciadaUtc`, publica la primera pregunta y emite `salaIniciada` + `preguntaActual` | `NO_AUTORIZADO`, `SALA_NO_ENCONTRADA`, `SALA_YA_INICIADA`, `SIN_PREGUNTAS` |
| `AvanzarPregunta(preguntaId?)` | UUID opcional | Cierra la pregunta vigente y publica la siguiente (o la indicada). Emite `preguntaActual`. Si no se indica, avanza en orden | `NO_AUTORIZADO`, `PREGUNTA_INEXISTENTE`, `SALA_NO_INICIADA`, `SALA_FINALIZADA` |
| `FinalizarSala(motivo?)` | `Docente` o `TiempoAgotado` | Pasa la sala a `Finalizada`, fija `finalizadaUtc` y emite `salaFinalizada` una sola vez | `NO_AUTORIZADO`, `SALA_NO_ENCONTRADA` |

`IniciarSala`, `AvanzarPregunta` y `FinalizarSala` deben ser **idempotentes**: repetir la
llamada con el mismo estado no debe duplicar eventos ni romper la sala. Si la sala ya está
en el estado pedido, el servidor responde éxito y, si corresponde, reemite el estado al
llamador.

`SIN_PREGUNTAS` no está en el catálogo de respuestas porque solo aplica al docente: la
evaluación publicada no tiene preguntas. Se documenta acá y se agrega al catálogo de
errores de control.

### 5.5 Estado de implementación y brechas

| Elemento | Código actual | Contrato v1 | Brecha / tarea |
| --- | --- | --- | --- |
| `UnirseASala` | `Task UnirseASala(int pin)`; `HubException`; grupo `salaId.ToString()` | Método estable; idealmente idempotente para reconexión y con códigos estables | SA-04; ajuste en S2-SA-02 |
| Claim de institución | `SalaHub` lee `InstitucionId` | Identidad emite `institucion_id` | SA-04 / SA-06 |
| `UnirseASalaDocente` | No existe | Método nuevo | SA-06 |
| `EnviarRespuesta` | No existe | Método + payload + `ResultadoEnvioRespuesta` | S2-SA-01 |
| `ObtenerEstadoSala` | No existe | Método nuevo | S2-SA-02 |
| `IniciarSala` / `AvanzarPregunta` / `FinalizarSala` | No existen | Métodos del docente | SA-06 |
| Eventos del hub | No existen; el fake de tests simula `alumnoIngreso` con un string | 7 eventos con payload objeto | SA-06 / FE2-02 |
| Tabla `RespuestaSala` | No existe | Índices únicos e idempotencia | S2-SA-01 |
| Contrato escrito | Este documento | Congelado | SA-05 |

## 6. Eventos del servidor

Todos los eventos son mensajes servidor → cliente. El cliente los registra con
`conexion.escuchar("nombreEvento", manejador)` (wrapper de `hub.on`). Los nombres son
sensibles a mayúsculas/minúsculas en el contrato, aunque SignalR los resuelva sin
distinción: el cliente debe usar exactamente el nombre documentado.

| Evento | Destinatarios | Cuándo | Tarea |
| --- | --- | --- | --- |
| `alumnoIngreso` | `{salaId}:docentes` | Un alumno se une o se reconecta | SA-06 |
| `salaIniciada` | `{salaId}` | El docente inicia la sala | SA-06 |
| `preguntaActual` | `{salaId}` | Al iniciar, avanzar o recuperar estado | SA-06 / S2-SA-02 |
| `respuestaRegistrada` | `{salaId}:docentes` | Se acepta una respuesta nueva | S2-SA-01 |
| `estadoSala` | Solo al llamador | Al unirse, reconectar o pedir estado | S2-SA-02 |
| `salaFinalizada` | `{salaId}` | El docente finaliza o vence el tiempo | SA-06 / S2-SA-01 |
| `error` | Solo al llamador | Error asíncrono no ligado a una invocación | SA-06 |

### 6.1 `alumnoIngreso`

Notifica al docente que un alumno está conectado. No se envía a los demás alumnos.

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `salaId` | UUID | Sala |
| `alumnoId` | UUID | Identificador interno del alumno (solo docentes) |
| `alias` | string | Alias visible, ej. `Alumno 1`; nunca nombre, email ni legajo |
| `totalConectados` | integer | Cantidad de alumnos conectados en ese momento |
| `ingresoUtc` | date-time | Hora del servidor |

```json
{
  "salaId": "11111111-1111-4111-8111-111111111111",
  "alumnoId": "22222222-2222-4222-8222-222222222222",
  "alias": "Alumno 1",
  "totalConectados": 12,
  "ingresoUtc": "2026-10-26T13:59:58.120Z"
}
```

> El fake actual de `frontend/src/shared/realtime/conexionFalsa.test.ts` simula
> `alumnoIngreso` con el string `"Alumno 1 A"`. Al implementar SA-06 hay que actualizar
> el test y el manejador al payload objeto de esta tabla.

### 6.2 `salaIniciada`

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `salaId` | UUID | Sala |
| `iniciadaUtc` | date-time | Inicio autoritativo |
| `totalPreguntas` | integer | Cantidad de preguntas de la evaluación publicada |
| `fechaLimiteUtc` | date-time | Vencimiento de la sala |

```json
{
  "salaId": "11111111-1111-4111-8111-111111111111",
  "iniciadaUtc": "2026-10-26T14:00:00.000Z",
  "totalPreguntas": 2,
  "fechaLimiteUtc": "2026-10-26T14:30:00.000Z"
}
```

### 6.3 `preguntaActual`

Es el evento que habilita la pantalla de examen. **No incluye `esCorrecta` ni
`rubrica`**: el alumno no debe poder inferir la respuesta correcta ni los criterios de
corrección.

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `salaId` | UUID | Sala |
| `preguntaId` | UUID | Pregunta vigente |
| `indice` | integer | Posición 1-based |
| `totalPreguntas` | integer | Total de la evaluación |
| `tipo` | `OpcionMultiple` o `Abierta` | Tipo |
| `enunciado` | string | Consigna |
| `opciones` | array o `null` | Solo para `OpcionMultiple`; cada ítem `{ id, texto }` |
| `puntajeMaximo` | number | Puntaje máximo |
| `fechaInicioUtc` | date-time | Inicio de la pregunta |
| `fechaLimiteUtc` | date-time | Fin de la pregunta (cronómetro del servidor) |

```json
{
  "salaId": "11111111-1111-4111-8111-111111111111",
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "indice": 1,
  "totalPreguntas": 2,
  "tipo": "OpcionMultiple",
  "enunciado": "¿Qué imprime Console.WriteLine(2 + 3)?",
  "opciones": [
    { "id": "7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d", "texto": "5" },
    { "id": "8b2c3d4e-5f60-4b7c-9d8e-0f1a2b3c4d5e", "texto": "23" }
  ],
  "puntajeMaximo": 2,
  "fechaInicioUtc": "2026-10-26T14:00:00.000Z",
  "fechaLimiteUtc": "2026-10-26T14:05:00.000Z"
}
```

Para una pregunta `Abierta`, `opciones` es `null` (o se omite) y no se envía la rúbrica.
El `fechaLimiteUtc` es la única fuente de verdad para el cronómetro; el cliente no debe
calcular el cierre solo con su reloj local.

### 6.4 `respuestaRegistrada`

Se emite solo cuando se persiste una respuesta **nueva** (`Aceptada`). No se emite en
`Duplicada` ni en `Rechazada`. No incluye el texto de la respuesta: el docente ve el
avance, no el contenido en tiempo real.

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `salaId` | UUID | Sala |
| `alumnoId` | UUID | Alumno que respondió |
| `preguntaId` | UUID | Pregunta respondida |
| `secuencia` | integer | Secuencia aceptada |
| `estado` | `Aceptada` | Estado (siempre `Aceptada` en este evento) |
| `recibidaUtc` | date-time | Hora del servidor |

```json
{
  "salaId": "11111111-1111-4111-8111-111111111111",
  "alumnoId": "22222222-2222-4222-8222-222222222222",
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "secuencia": 1,
  "estado": "Aceptada",
  "recibidaUtc": "2026-10-26T14:03:11.482Z"
}
```

### 6.5 `estadoSala`

Se envía solo al llamador (no al grupo) después de `UnirseASala`,
`UnirseASalaDocente`, `ObtenerEstadoSala` o una reconexión exitosa. Es la base para
restaurar la pantalla sin perder respuestas.

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `salaId` | UUID | Sala |
| `estado` | `Activa`, `EnCurso` o `Finalizada` | Estado de la sala |
| `preguntaActual` | objeto o `null` | Mismo payload de `preguntaActual`; `null` si no comenzó o terminó |
| `ultimaSecuencia` | integer | Última secuencia aceptada del alumno; 0 si no respondió nada |
| `preguntasRespondidas` | array de UUID | Preguntas ya respondidas por el alumno |
| `servidorUtc` | date-time | Hora del servidor para calcular el tiempo restante |
| `fechaLimiteUtc` | date-time o `null` | Vencimiento de la sala |

```json
{
  "salaId": "11111111-1111-4111-8111-111111111111",
  "estado": "EnCurso",
  "preguntaActual": {
    "salaId": "11111111-1111-4111-8111-111111111111",
    "preguntaId": "9c3d4e5f-6071-4c8d-8e9f-1a2b3c4d5e6f",
    "indice": 2,
    "totalPreguntas": 2,
    "tipo": "Abierta",
    "enunciado": "Explicá qué es una variable.",
    "opciones": null,
    "puntajeMaximo": 4,
    "fechaInicioUtc": "2026-10-26T14:05:00.000Z",
    "fechaLimiteUtc": "2026-10-26T14:10:00.000Z"
  },
  "ultimaSecuencia": 1,
  "preguntasRespondidas": [
    "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60"
  ],
  "servidorUtc": "2026-10-26T14:06:30.000Z",
  "fechaLimiteUtc": "2026-10-26T14:30:00.000Z"
}
```

Si el alumno ya respondió la pregunta vigente, `preguntasRespondidas` la incluye y el
cliente debe mostrar la vista de “respuesta enviada”, no volver a habilitar el envío.

Para un docente, `ultimaSecuencia` es `0` y `preguntasRespondidas` es `[]`: el panel
docente no tiene una secuencia propia y el avance real llega por `respuestaRegistrada`.

### 6.6 `salaFinalizada`

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `salaId` | UUID | Sala |
| `finalizadaUtc` | date-time | Cierre autoritativo |
| `motivo` | `Docente` o `TiempoAgotado` | Causa del cierre |

```json
{
  "salaId": "11111111-1111-4111-8111-111111111111",
  "finalizadaUtc": "2026-10-26T14:30:00.000Z",
  "motivo": "TiempoAgotado"
}
```

Después de este evento el cliente cierra la pantalla de examen, deshabilita el envío y
muestra “examen finalizado”. Un `EnviarRespuesta` **nuevo** posterior devuelve
`SALA_FINALIZADA` o `TIEMPO_AGOTADO`; un reintento de una respuesta ya aceptada sigue
devolviendo `Duplicada`.

### 6.7 `error`

Canal para errores asíncronos que no corresponden a una invocación concreta (por ejemplo,
la sala se cerró mientras el servidor procesaba un lote, o se superó un límite de envío).
Para errores de una invocación, usar el resultado o el `HubException` según la sección 5.

| Campo | Tipo | Descripción |
| --- | --- | --- |
| `codigo` | string | Código estable del catálogo (sección 8.3) |
| `mensaje` | string | Texto legible para el usuario |
| `reintentable` | boolean | Si el cliente puede reintentar |
| `correlationId` | string | Identificador de correlación para soporte |
| `detalles` | objeto o `null` | Datos adicionales no sensibles |

```json
{
  "codigo": "SALA_FINALIZADA",
  "mensaje": "La sala finalizó y no admite más respuestas.",
  "reintentable": false,
  "correlationId": "0f7c2f0e-9a1b-4c2d-8e3f-4a5b6c7d8e9f",
  "detalles": {
    "salaId": "11111111-1111-4111-8111-111111111111"
  }
}
```

## 7. Idempotencia, secuencia y concurrencia

### 7.1 Los tres mecanismos y por qué existen

| Mecanismo | Qué resuelve | Dónde vive |
| --- | --- | --- |
| `idempotencyKey` | Reintentos por timeout, red caída o acuse perdido: el mismo envío lógico no se duplica | Columna única por `(SalaId, AlumnoId, IdempotencyKey)` |
| Índice único `(SalaId, AlumnoId, PreguntaId)` | Una sola respuesta por pregunta y alumno, incluso ante dos claves distintas | Índice único en `RespuestaSala` |
| `secuencia` | Orden y detección de huecos/duplicados por alumno | Contador por `(SalaId, AlumnoId)` y columna única |

Los tres son necesarios. La clave sola no impide que un cliente invente otra clave para
la misma pregunta; el índice único solo no distingue un reintento legítimo de un intento
nuevo; la secuencia sola no sobrevive a un reintento con la misma posición.

### 7.2 Reglas normativas

1. El cliente genera `idempotencyKey` con `crypto.randomUUID()` **una vez** por respuesta
   lógica. En reintentos de red usa exactamente la misma clave.
2. El cliente mantiene `ultimaSecuencia` por sala. La primera respuesta usa `1`; cada
   respuesta **aceptada** incrementa en 1.
3. Un rechazo (`Rechazada`) **no consume** secuencia y **no persiste la clave**. Para
   corregir el payload, el cliente usa la misma `secuencia`; se recomienda una
   `idempotencyKey` nueva por claridad, aunque reutilizar la anterior también es válido
   porque nunca llegó a guardarse.
4. Un duplicado (`Duplicada`) **sí** confirma que la secuencia fue consumida; el cliente
   actualiza `ultimaSecuencia` si hacía falta y no reintenta más.
5. No se puede reutilizar una `idempotencyKey` para otra pregunta, otra respuesta u otra
   secuencia. Si la clave existe con un payload canónico distinto:
   `IDEMPOTENCY_KEY_REUTILIZADA`.
6. La primera respuesta válida a una pregunta es la definitiva. Un segundo intento con
   otra clave devuelve `RESPUESTA_YA_REGISTRADA`; no se sobrescribe.
7. Los huecos de secuencia se rechazan (`SECUENCIA_FUERA_DE_ORDEN`). Si el cliente perdió
   un acuse, primero se sincroniza con `ObtenerEstadoSala` y reenvía lo pendiente con la
   clave y secuencia originales.
8. La comparación de payload canónico usa `preguntaId`, `respuesta` normalizada (trim) y
   `secuencia`. El orden de las propiedades JSON no importa.
9. La idempotencia se conserva mientras exista la sala y, como mínimo, hasta que la
   entrega se envíe a Evaluaciones. No se borra al cerrar la conexión.

### 7.3 Escenarios

| Escenario | `idempotencyKey` | `secuencia` | Resultado |
| --- | --- | --- | --- |
| Primer envío de la pregunta 1 | `K1` | 1 | `Aceptada`; se persiste |
| Reintento tras timeout del mismo envío | `K1` | 1 | `Duplicada`; mismo `recibidaUtc`; sin evento |
| Reintento con `K1` pero respuesta distinta | `K1` | 1 | `Rechazada` / `IDEMPOTENCY_KEY_REUTILIZADA` |
| Primera respuesta a la pregunta 2 | `K2` | 2 | `Aceptada` |
| Segunda respuesta a la pregunta 1 con clave nueva | `K3` | 3 | `Rechazada` / `RESPUESTA_YA_REGISTRADA` |
| Envío con secuencia 5 cuando la última es 3 | `K4` | 5 | `Rechazada` / `SECUENCIA_FUERA_DE_ORDEN` |
| Envío con secuencia 3 cuando la última es 3 y clave nueva | `K5` | 3 | `Rechazada` / `SECUENCIA_FUERA_DE_ORDEN` |
| Respuesta inválida (texto vacío) | `K6` | 4 | `Rechazada` / `RESPUESTA_INVALIDA`; no consume secuencia |
| Corrección de la respuesta inválida | `K7` | 4 | `Aceptada` |
| Dos envíos concurrentes idénticos | `K8` | 5 | Uno `Aceptada`; el otro `Duplicada` |
| Dos envíos concurrentes, misma pregunta, claves distintas | `K9` y `K10` | 5 | Uno `Aceptada`; el otro `RESPUESTA_YA_REGISTRADA` |

### 7.4 Algoritmo del cliente

```ts
// Una sola instancia por sala; ultimaSecuencia se restaura con estadoSala.
let ultimaSecuencia = 0;

async function enviarRespuesta(preguntaId: string, respuesta: string) {
  const idempotencyKey = crypto.randomUUID();
  const secuencia = ultimaSecuencia + 1;

  for (let intento = 1; intento <= 3; intento++) {
    try {
      const r = await conexion.invocar("EnviarRespuesta", {
        preguntaId,
        respuesta,
        idempotencyKey,
        secuencia,
      });

      if (r.estado === "Aceptada" || r.estado === "Duplicada") {
        ultimaSecuencia = secuencia;
        return r;
      }

      if (r.error?.reintentable) {
        await esperar(backoff(intento)); // 1s, 2s, 4s...
        continue;
      }

      throw new Error(r.error?.mensaje ?? "No se pudo enviar la respuesta.");
    } catch (e) {
      // Error de red: el servidor pudo haber aceptado. Reintentar con la MISMA clave.
      if (intento === 3) throw e;
      await esperar(backoff(intento));
    }
  }
}
```

Reglas del cliente:

- No generar una clave nueva al reintentar por red.
- No enviar la siguiente respuesta hasta recibir `Aceptada` o `Duplicada`.
- Si el servidor responde `SECUENCIA_FUERA_DE_ORDEN`, llamar `ObtenerEstadoSala`, fijar
  `ultimaSecuencia` y reenviar lo pendiente con su clave original.
- Si responde `RESPUESTA_YA_REGISTRADA`, considerar la pregunta como respondida y refrescar
  la vista; no reintentar con otra clave.
- Si responde `PREGUNTA_NO_VIGENTE`, refrescar `preguntaActual`; no reintentar la respuesta
  anterior.
- Si responde `IDEMPOTENCY_KEY_REUTILIZADA`, es un bug del cliente: generar una clave nueva
  solo si la respuesta es realmente otra; si es un reintento, restaurar el payload original.

### 7.5 Reconexión

Al reconectar, el cliente no sabe si el último `invoke` llegó al servidor. El orden seguro
es:

1. `ObtenerEstadoSala()` (o re-`UnirseASala` si perdió el `salaId`).
2. Leer `ultimaSecuencia`, `preguntasRespondidas` y `preguntaActual`.
3. Si había un envío en vuelo, reenviarlo con la misma `idempotencyKey` y `secuencia`.
4. Si el servidor responde `Duplicada`, el envío ya estaba persistido; continuar.
5. Si responde `SECUENCIA_FUERA_DE_ORDEN`, sincronizar de nuevo y resolver el hueco.

El servidor no debe crear una respuesta nueva por una reconexión. `UnirseASala` repetido
para el mismo alumno y la misma sala es un éxito idempotente.

## 8. Errores

### 8.1 Capas de error

| Capa | Cuándo | Cómo se manifiesta | Quién lo maneja |
| --- | --- | --- | --- |
| Conexión/handshake | Token ausente, vencido, rol incorrecto, ruta mal configurada | HTTP 401/403/404 antes de abrir el hub; `onclose` | Frontend: refrescar sesión o corregir configuración |
| Negocio en `EnviarRespuesta` | Sala cerrada, pregunta no vigente, secuencia incorrecta, etc. | `ResultadoEnvioRespuesta.estado = Rechazada` con `error` | Frontend: decidir según `reintentable` |
| Negocio en métodos de control | PIN inválido, sala llena, no autorizado, etc. | `HubException` con `CODIGO: mensaje` | Frontend: separar código y mostrar mensaje |
| Asíncrono | Error del servidor no ligado a una invocación | Evento `error` con `ErrorHub` | Frontend: mostrar aviso y registrar `correlationId` |
| Inesperado | Excepción no controlada, base caída, timeout interno | `ERROR_INTERNO` por el canal que corresponda | Frontend: reintentar con backoff; escalar con `correlationId` |

Regla de oro: **nunca** se devuelve al cliente un stack trace, un mensaje de PostgreSQL ni
un detalle de infraestructura. El detalle técnico va al log con `correlationId`; al cliente
le llega un mensaje legible.

### 8.2 Objeto `ErrorHub`

```json
{
  "codigo": "SECUENCIA_FUERA_DE_ORDEN",
  "mensaje": "La secuencia recibida no es la esperada.",
  "reintentable": true,
  "correlationId": "0f7c2f0e-9a1b-4c2d-8e3f-4a5b6c7d8e9f",
  "detalles": {
    "secuenciaEsperada": 3,
    "secuenciaRecibida": 5
  }
}
```

| Campo | Tipo | Obligatorio | Descripción |
| --- | --- | --- | --- |
| `codigo` | string | Sí | Código estable del catálogo; no se traduce ni cambia entre versiones |
| `mensaje` | string | Sí | Texto legible en español para mostrar al usuario |
| `reintentable` | boolean | Sí | `true` si el mismo envío puede reintentarse sin cambios |
| `correlationId` | string | Sí | Identificador para cruzar con logs y soporte |
| `detalles` | objeto o `null` | No | Datos no sensibles (secuencia esperada, pregunta actual, etc.) |

`detalles` nunca incluye la respuesta correcta, la rúbrica ni datos de otro alumno.

### 8.3 Catálogo de códigos

| Código | Canal | Cuándo | Reintentable | Acción del cliente | Equivalente HTTP |
| --- | --- | --- | --- | --- | --- |
| `NO_AUTENTICADO` | Handshake | Falta el token, está vencido o la firma es inválida | No | Refrescar sesión y reconectar | 401 |
| `NO_AUTORIZADO` | Handshake / `HubException` | El rol no puede ejecutar el método o no pertenece a la sala | No | Mostrar “no autorizado” | 403 |
| `SALA_NO_ENCONTRADA` | `HubException` / `Rechazada` | PIN inexistente, sala inactiva/eliminada o PIN vencido. El handler legado de SA-04 también usa este mensaje para salas finalizadas | No | Pedir PIN de nuevo | 404 |
| `SALA_OTRA_INSTITUCION` | `HubException` | La sala pertenece a otro tenant | No | No revelar detalles; cerrar | 403 |
| `SALA_LLENA` | `HubException` | `CantidadParticipantes >= CapacidadMaxima` | No | Avisar al docente | 409 |
| `ALUMNO_YA_EN_SALA` | `HubException` (solo SA-04 legado) | El alumno ya estaba registrado; en reconexión debe tratarse como éxito | No | Si el `salaId` coincide, continuar; si no, cerrar | 409 |
| `SALA_FINALIZADA` | `HubException` / `Rechazada` / `error` | La sala ya cerró | No | Mostrar resultados; no enviar | 410 |
| `SALA_NO_INICIADA` | `Rechazada` | Se intenta responder antes de `salaIniciada` | Sí, más tarde | Esperar `salaIniciada` y reintentar con la misma clave | 409 |
| `SALA_YA_INICIADA` | `HubException` | `IniciarSala` sobre una sala ya iniciada | No | Reemitir estado; no repetir la acción | 409 |
| `ALUMNO_NO_EN_SALA` | `Rechazada` / `HubException` | La conexión no está unida a la sala | Sí, tras rejoin | `UnirseASala` y reenviar con la misma clave | 403 |
| `PREGUNTA_INEXISTENTE` | `Rechazada` | El `preguntaId` no pertenece a la evaluación publicada | No | Refrescar estado; no inventar IDs | 404 |
| `PREGUNTA_NO_VIGENTE` | `Rechazada` | La pregunta ya se cerró o se avanzó | No | Refrescar `preguntaActual`; no reintentar la anterior | 409 |
| `RESPUESTA_INVALIDA` | `Rechazada` | Texto vacío, demasiado largo, opción inexistente o tipo incorrecto | No | Corregir y usar nueva clave, misma secuencia | 400 |
| `RESPUESTA_YA_REGISTRADA` | `Rechazada` | Ya hay una respuesta para esa pregunta con otra clave | No | Tratar la primera como definitiva; refrescar | 409 |
| `IDEMPOTENCY_KEY_REUTILIZADA` | `Rechazada` | La clave existe con otro payload canónico | No | Generar clave nueva solo si es otra respuesta; si es reintento, restaurar el payload | 409 |
| `SECUENCIA_FUERA_DE_ORDEN` | `Rechazada` | `secuencia != ultimaSecuencia + 1` y no es duplicado | Sí, tras sincronizar | `ObtenerEstadoSala` y reenviar pendientes | 409 |
| `TIEMPO_AGOTADO` | `Rechazada` / `error` | Venció la pregunta o la sala | No | Cerrar el envío y mostrar fin | 410 |
| `PAYLOAD_INVALIDO` | `Rechazada` | Faltan campos, tipos incorrectos o UUID inválido | No | Corregir el cliente; no reintentar igual | 400 |
| `LIMITE_ENVIOS` | `error` | Rate limit por conexión/alumno | Sí, con backoff | Esperar y reintentar con la misma clave | 429 |
| `SIN_PREGUNTAS` | `HubException` | La evaluación publicada no tiene preguntas | No | El docente debe corregir la evaluación | 409 |
| `ERROR_INTERNO` | `Rechazada` / `HubException` / `error` | Fallo no controlado | Sí, con backoff | Reintentar la misma clave; si persiste, soporte con `correlationId` | 500 |

> Los “equivalentes HTTP” son orientativos para discusiones de producto y pruebas. SignalR
> no devuelve códigos HTTP por invocación; el código estable es `codigo`.

### 8.4 Mapeo de los mensajes actuales de SA-04

Mientras `UnirseASala` no devuelva el resultado estructurado, el cliente puede mapear los
mensajes actuales de `SalaHub`:

| Mensaje actual | Código objetivo | Comentario |
| --- | --- | --- |
| `Alumno no identificado.` | `NO_AUTENTICADO` | Falta el claim `sub`/`NameIdentifier` |
| `Institución no válida.` | `NO_AUTORIZADO` | El hub busca el claim `InstitucionId`, pero Identidad emite `institucion_id`; ver brecha en 2.2 |
| `No tienes permiso para ingresar a esta sala.` | `SALA_OTRA_INSTITUCION` | `UnauthorizedAccessException` del handler |
| `La sala no existe, está inactiva o venció.` | `SALA_NO_ENCONTRADA` (o `SALA_FINALIZADA`) | `InvalidOperationException` del handler; el mensaje legado no distingue ambos casos |
| `El alumno ya está registrado en la sala.` | `ALUMNO_YA_EN_SALA` | **No es error en reconexión**: tratar como éxito si el `salaId` coincide; S2-SA-02 lo corrige |
| `La sala ha alcanzado su capacidad máxima.` | `SALA_LLENA` | `InvalidOperationException` del handler |
| `No se pudo generar un PIN disponible después de 30 intentos.` | `ERROR_INTERNO` | Es un error de `POST /api/v1/salas`, no del hub |

Este mapeo es temporal y no debe quedar hardcodeado en el frontend más allá de la
transición. La meta es que el servidor emita `CODIGO: mensaje` o el resultado estructurado.

### 8.5 Política de reintentos

| `reintentable` | Qué significa | Qué debe hacer el cliente |
| --- | --- | --- |
| `true` | El mismo envío puede funcionar más tarde sin cambiar el payload | Reintentar con la **misma** `idempotencyKey` y `secuencia`, con backoff exponencial y jitter |
| `false` | Reintentar el mismo payload no va a cambiar el resultado | Corregir el estado o el payload; no hacer retry ciego |

Backoff recomendado: 1 s, 2 s, 4 s, 8 s (máximo 4 intentos), con jitter de hasta 500 ms.
Ante `SECUENCIA_FUERA_DE_ORDEN`, no hacer backoff ciego: primero `ObtenerEstadoSala`.
Ante `ERROR_INTERNO`, reintentar hasta 3 veces; si persiste, mostrar el `correlationId`.

Nunca reintentar automáticamente:

- `RESPUESTA_YA_REGISTRADA` (la primera respuesta es la válida);
- `IDEMPOTENCY_KEY_REUTILIZADA` (es un bug de cliente);
- `PREGUNTA_NO_VIGENTE` (la pregunta cambió);
- `TIEMPO_AGOTADO` / `SALA_FINALIZADA` (el examen terminó);
- `NO_AUTENTICADO` / `NO_AUTORIZADO` (requiere intervención de sesión).

### 8.6 Trazabilidad

Cada invocación de `EnviarRespuesta` debe registrar, como mínimo:

- `correlationId` (generado por el servidor si no viene en el contexto);
- `connectionId` (no exponerlo al cliente);
- `salaId`, `alumnoId`, `preguntaId`;
- `idempotencyKey`, `secuencia`;
- resultado (`Aceptada`/`Duplicada`/`Rechazada`) y, si aplica, `codigo`;
- latencia de persistencia.

El `correlationId` que se devuelve al cliente debe ser el mismo que se escribe en el log.
No registrar el token, la respuesta completa del alumno ni datos personales.

## 9. Persistencia y trazabilidad (guía para S2-SA-01)

Esta sección no es el contrato de red, pero fija las invariantes que el contrato necesita
para cumplirse. La implementación concreta (nombres de tabla, migración, EF Core) es de
S2-SA-01.

### 9.1 Campos mínimos de `RespuestaSala`

| Campo | Tipo | Notas |
| --- | --- | --- |
| `Id` | UUID | PK |
| `InstitucionId` | UUID | Obligatorio para el aislamiento multi-tenant |
| `SalaId` | UUID | FK a `Sala` |
| `AlumnoId` | UUID | Del token, nunca del payload |
| `PreguntaId` | UUID | De la evaluación publicada |
| `Respuesta` | text | Texto u opción serializada; validada contra el tipo |
| `IdempotencyKey` | UUID | Clave del cliente |
| `Secuencia` | integer | Contador por sala/alumno |
| `RecibidaUtc` | date-time | Hora del servidor; se devuelve igual en `Duplicada` |

### 9.2 Índices únicos mínimos

```sql
-- Una respuesta por pregunta y alumno
CREATE UNIQUE INDEX ux_respuesta_sala_pregunta
  ON "RespuestaSala" ("SalaId", "AlumnoId", "PreguntaId");

-- Una clave de idempotencia por alumno y sala
CREATE UNIQUE INDEX ux_respuesta_sala_idempotency
  ON "RespuestaSala" ("SalaId", "AlumnoId", "IdempotencyKey");

-- Una secuencia por alumno y sala
CREATE UNIQUE INDEX ux_respuesta_sala_secuencia
  ON "RespuestaSala" ("SalaId", "AlumnoId", "Secuencia");
```

Los nombres son ilustrativos; lo obligatorio son las combinaciones. Si la tabla usa
`RespuestasSala` en plural, ajustar el SQL, no la semántica. Los índices deben crearse en
una migración de EF Core, no con SQL manual en producción.

### 9.3 Carreras esperables

| Carrera | Resultado correcto |
| --- | --- |
| Dos envíos idénticos (misma clave y secuencia) | Uno `Aceptada`; el otro `Duplicada` |
| Dos claves distintas, misma pregunta | Uno `Aceptada`; el otro `RESPUESTA_YA_REGISTRADA` |
| Dos preguntas distintas, misma secuencia | Uno `Aceptada`; el otro `SECUENCIA_FUERA_DE_ORDEN` |
| Reintento mientras la transacción original no commiteó | El segundo espera o recibe violación única; al releer devuelve `Duplicada` |

La transacción debe incluir la inserción de la respuesta y la actualización de
`ultimaSecuencia` (si se guarda en `ParticipanteSala` o en una tabla de estado). Si se
calcula `MAX(Secuencia)` al vuelo, el índice único sigue siendo la defensa final.

### 9.4 Retención

- La clave de idempotencia se conserva mientras exista la sala.
- Al cerrar la sala y enviar la entrega a Evaluaciones, se conserva al menos hasta que la
  entrega esté confirmada y por el plazo que defina la política de retención del proyecto.
- No se reutilizan claves entre salas: el ámbito es `(SalaId, AlumnoId)`.

---

## 10. Versionado y cambios

- La versión del contrato está en el nombre: `sala-hub-v1`.
- **Cambios aditivos permitidos en v1:** campos opcionales nuevos, eventos nuevos que el
  cliente puede ignorar, códigos de error nuevos documentados. El cliente debe ignorar
  campos y eventos desconocidos.
- **Cambios rupturistas:** renombrar/quitar campos, cambiar tipos, cambiar la semántica de
  `secuencia` o de los estados. Requieren `v2` (nueva ruta o nuevos nombres de método) y
  un período de convivencia.
- Todo cambio actualiza en el **mismo PR**:
  1. este documento;
  2. los JSON Schema de `contracts/schemas/`;
  3. los ejemplos de `contracts/signalr/ejemplos/`;
  4. las pruebas de Postman/Newman y, si aplica, Cypress;
  5. el código de `SalaHub` y del frontend.
- El contrato lo mantiene Sala en Vivo; los cambios de payload se revisan con Evaluaciones
  (dueño del esquema de preguntas) y Frontend 2 (consumidor).

---

## 11. Criterios de aceptación de SA-05

- [x] Métodos del hub documentados con rol, payload, retorno y efectos.
- [x] Eventos documentados con destinatarios y payload.
- [x] Payload de envío de respuestas con `preguntaId`, `respuesta`, `idempotencyKey` y
      `secuencia`, con tipo, obligatoriedad y reglas.
- [x] Semántica de idempotencia, secuencia y concurrencia con escenarios.
- [x] Catálogo de errores con código, cuándo ocurre, si es reintentable y acción del
      cliente.
- [x] JSON Schema del request y del resultado en `contracts/schemas/`.
- [x] Ejemplos ejecutables en `contracts/signalr/ejemplos/`.
- [x] Brechas con la implementación actual identificadas (SA-04, SA-06, S2-SA-01,
      S2-SA-02).
- [ ] Revisión de Evaluaciones y Frontend 2 (pendiente de PR).
- [ ] Implementación de `EnviarRespuesta` y de los eventos (S2-SA-01 / SA-06).

---

## 12. Referencias

- Hub actual: `services/sala-en-vivo/src/PEI.SalaEnVivo.Api/Hubs/SalaHub.cs`
- Configuración JWT y ruta del hub: `services/sala-en-vivo/src/PEI.SalaEnVivo.Api/Program.cs`
- Gateway: `gateway/src/PEI.Gateway/appsettings.json` (ruta `sala-hub-route`)
- Cliente SignalR: `frontend/src/shared/realtime/conexion.ts`,
  `frontend/src/shared/realtime/tipos.ts`,
  `frontend/src/shared/realtime/useConexionSala.ts`
- Esquema de preguntas: `contracts/schemas/preguntas-v1.schema.json`
- OpenAPI de evaluaciones: `contracts/openapi/evaluaciones-v1.json`
- Backlog: SA-05, SA-06, S2-SA-01, S2-SA-02, FE2-02, S2-FE-02, S2-QA-01.

---

## Apéndice A — Tipos TypeScript de referencia

```ts
export type EstadoEnvio = "Aceptada" | "Duplicada" | "Rechazada";

export type CodigoError =
  | "NO_AUTENTICADO"
  | "NO_AUTORIZADO"
  | "SALA_NO_ENCONTRADA"
  | "SALA_OTRA_INSTITUCION"
  | "SALA_LLENA"
  | "ALUMNO_YA_EN_SALA"
  | "SALA_FINALIZADA"
  | "SALA_NO_INICIADA"
  | "SALA_YA_INICIADA"
  | "ALUMNO_NO_EN_SALA"
  | "PREGUNTA_INEXISTENTE"
  | "PREGUNTA_NO_VIGENTE"
  | "RESPUESTA_INVALIDA"
  | "RESPUESTA_YA_REGISTRADA"
  | "IDEMPOTENCY_KEY_REUTILIZADA"
  | "SECUENCIA_FUERA_DE_ORDEN"
  | "TIEMPO_AGOTADO"
  | "PAYLOAD_INVALIDO"
  | "LIMITE_ENVIOS"
  | "SIN_PREGUNTAS"
  | "ERROR_INTERNO";

export interface ErrorHub {
  codigo: CodigoError;
  mensaje: string;
  reintentable: boolean;
  correlationId: string;
  detalles?: Record<string, unknown> | null;
}

export interface EnviarRespuestaRequest {
  preguntaId: string;
  respuesta: string;
  idempotencyKey: string;
  secuencia: number;
}

export interface ResultadoEnvioRespuesta {
  estado: EstadoEnvio;
  preguntaId: string | null;
  idempotencyKey: string | null;
  secuencia: number | null;
  recibidaUtc: string;
  error: ErrorHub | null;
}

export interface OpcionEvento {
  id: string;
  texto: string;
}

export interface PreguntaActualEvento {
  salaId: string;
  preguntaId: string;
  indice: number;
  totalPreguntas: number;
  tipo: "OpcionMultiple" | "Abierta";
  enunciado: string;
  opciones: OpcionEvento[] | null;
  puntajeMaximo: number;
  fechaInicioUtc: string;
  fechaLimiteUtc: string;
}
```

## Apéndice B — Pseudocódigo del servidor

```csharp
public async Task<ResultadoEnvioRespuesta> EnviarRespuesta(EnviarRespuestaRequest req)
{
    var ct = Context.ConnectionAborted;
    var alumnoId = ObtenerAlumnoId(Context.User);       // claim, no payload
    var institucionId = ObtenerInstitucionId(Context.User);
    var sala = await _salas.ObtenerSalaDeConexionAsync(Context.ConnectionId, ct);

    if (sala is null) return Rechazar("ALUMNO_NO_EN_SALA", reintentable: true);
    if (sala.InstitucionId != institucionId) return Rechazar("NO_AUTORIZADO");

    // Idempotencia primero: un reintento ya aceptado responde Duplicada aunque la
    // pregunta o la sala se hayan cerrado después del envío original.
    var existentePorClave = await _respuestas.ObtenerPorClaveAsync(
        sala.Id, alumnoId, req.IdempotencyKey, ct);

    if (existentePorClave is not null)
    {
        return Coincide(existentePorClave, req)
            ? Resultado.Duplicada(existentePorClave)
            : Rechazar("IDEMPOTENCY_KEY_REUTILIZADA");
    }

    if (sala.Estado == "Finalizada") return Rechazar("SALA_FINALIZADA");
    if (sala.Estado != "EnCurso") return Rechazar("SALA_NO_INICIADA", reintentable: true);

    var pregunta = await _evaluaciones.ObtenerPreguntaVigenteAsync(sala, req.PreguntaId, ct);
    if (pregunta is null) return Rechazar("PREGUNTA_INEXISTENTE");
    if (!pregunta.EsVigente) return Rechazar("PREGUNTA_NO_VIGENTE");
    if (DateTime.UtcNow > pregunta.FechaLimiteUtc) return Rechazar("TIEMPO_AGOTADO");
    if (!EsRespuestaValida(pregunta, req.Respuesta)) return Rechazar("RESPUESTA_INVALIDA");

    if (await _respuestas.ExistePorPreguntaAsync(sala.Id, alumnoId, req.PreguntaId, ct))
        return Rechazar("RESPUESTA_YA_REGISTRADA");

    if (req.Secuencia != await _respuestas.UltimaSecuenciaAsync(sala.Id, alumnoId, ct) + 1)
        return Rechazar("SECUENCIA_FUERA_DE_ORDEN", reintentable: true);

    // Insertar + actualizar secuencia en una transacción.
    // Ante DbUpdateException por índice único: releer y resolver como Duplicada o conflicto.
    var respuesta = await _respuestas.InsertarAsync(sala, alumnoId, req, ct);
    await _hub.Clients.Group($"{sala.Id}:docentes")
        .SendAsync("respuestaRegistrada", EventoDe(respuesta), ct);

    return Resultado.Aceptada(respuesta);
}
```

Este pseudocódigo es ilustrativo: la transacción, el manejo de `DbUpdateException` y la
resolución de carreras deben implementarse en S2-SA-01, no copiarse sin esos controles.


