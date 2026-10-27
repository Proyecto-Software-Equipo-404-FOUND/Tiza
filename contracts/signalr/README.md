# Contratos de tiempo real — Sala en Vivo

Contrato congelado del hub SignalR `/hubs/sala` (tarea **SA-05**).

## Archivos

| Archivo | Contenido |
| --- | --- |
| `sala-hub-v1.md` | Contrato completo: conexión, métodos, eventos, payload de respuestas, idempotencia y errores |
| `../schemas/enviar-respuesta-sala-v1.schema.json` | JSON Schema del payload `EnviarRespuesta` |
| `../schemas/resultado-envio-respuesta-sala-v1.schema.json` | JSON Schema del resultado (`Aceptada`, `Duplicada`, `Rechazada`) |
| `ejemplos/` | Ejemplos listos para usar en Postman, tests o frontend |

## Resumen del payload de envío

```json
{
  "preguntaId": "5f1c1d9e-0a3b-4c11-9a52-1b2c3d4e5f60",
  "respuesta": "7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d",
  "idempotencyKey": "b3f1c2d4-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
  "secuencia": 1
}
```

- `respuesta` es el UUID de la opción si la pregunta es `OpcionMultiple`, o el texto si
  es `Abierta`.
- `idempotencyKey` se reutiliza en reintentos de la misma respuesta; no se reutiliza para
  otra respuesta.
- `secuencia` es un contador por `(salaId, alumnoId)`; se incrementa solo con
  `Aceptada`/`Duplicada`.

## Validación rápida

Desde la raíz del repositorio, verificar que todos los JSON sean válidos:

```powershell
Get-ChildItem contracts/signalr/ejemplos/*.json, contracts/schemas/*respuesta-sala-v1.schema.json |
  ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json | Out-Null; "OK $($_.Name)" }
```

Si el equipo usa Python con `jsonschema`:

```bash
python -m pip install jsonschema
python - <<'PY'
import json
from jsonschema import Draft202012Validator
schema = json.load(open("contracts/schemas/enviar-respuesta-sala-v1.schema.json", encoding="utf-8"))
data = json.load(open("contracts/signalr/ejemplos/enviar-respuesta-opcion-multiple.json", encoding="utf-8"))
Draft202012Validator(schema).validate(data)
print("OK")
PY
```

Con Node (sin instalar nada de forma permanente):

```powershell
npx.cmd --yes -p ajv-cli@5 -p ajv-formats ajv validate -c ajv-formats `
  -s contracts/schemas/enviar-respuesta-sala-v1.schema.json `
  -d contracts/signalr/ejemplos/enviar-respuesta-opcion-multiple.json `
  -d contracts/signalr/ejemplos/enviar-respuesta-abierta.json --spec=draft2020

npx.cmd --yes -p ajv-cli@5 -p ajv-formats ajv validate -c ajv-formats `
  -s contracts/schemas/resultado-envio-respuesta-sala-v1.schema.json `
  -d contracts/signalr/ejemplos/resultado-aceptada.json `
  -d contracts/signalr/ejemplos/resultado-duplicada.json `
  -d contracts/signalr/ejemplos/resultado-rechazada.json --spec=draft2020
```

Los cinco ejemplos de request/resultado ya pasaron esta validación. Los dos ejemplos de
eventos (`evento-pregunta-actual.json`, `evento-error.json`) no tienen schema en v1; se
validan solo como JSON.

## Relación con otras tareas

- **SA-04:** `UnirseASala` (implementado; ver brecha de reconexión en el contrato).
- **SA-06:** eventos `alumnoIngreso`, `salaIniciada`, `preguntaActual`,
  `respuestaRegistrada`, `salaFinalizada`.
- **S2-SA-01:** tabla `RespuestaSala`, índices únicos e implementación de
  `EnviarRespuesta`.
- **S2-SA-02:** `ObtenerEstadoSala` y recuperación al reconectar.
- **FE2-02 / S2-FE-02:** cliente SignalR y pantalla del alumno.
- **S2-QA-01:** pruebas de idempotencia con los ejemplos de esta carpeta.
