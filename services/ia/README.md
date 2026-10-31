# Microservicio de IA — PEI.IA

Microservicio encargado de la corrección automatizada y pedagógica de respuestas abiertas mediante modelos de lenguaje (LLM), utilizando el protocolo **Model Context Protocol (MCP)** para invocar herramientas pedagógicas y exponer recursos.

---

## 🏛️ Arquitectura del Servicio

El servicio está estructurado siguiendo principios de arquitectura limpia y desacoplamiento por capas:

```
services/ia/
├── src/
│   ├── PEI.IA.Api/              # Host Web API, endpoints REST (/api/v1/correccion), DI, Swagger
│   ├── PEI.IA.Application/      # Casos de uso (CorreccionService), DTOs, interfaces de clientes y prompts
│   ├── PEI.IA.Domain/           # Entidades (CalificacionSugerida, etc.) y reglas de dominio
│   ├── PEI.IA.Infrastructure/  # Clientes HTTP (OpenAiCompatibleClient, EvaluacionesHttpClient, McpToolExecutor)
│   └── PEI.IA.McpServer/        # Servidor MCP standalone (Tools y Recursos MCP)
├── datasets/                    # Datasets de evaluación y pruebas (dataset-correccion-v1.json)
├── prompts/                     # Plantillas de prompts pedagógicos (v1_correccion_pedagogica.md)
└── tests/
    └── PEI.IA.UnitTests/        # Pruebas unitarias de tools, servicios y clientes HTTP
```

---

## 🚀 Cambios Sprint 2 — Tarea S2-IA-01

En el marco de la tarea **S2-IA-01** (*Conectar tool MCP con endpoint interno real de Evaluaciones y exponer rúbricas como Recursos MCP*):

### 1. Integración con Microservicio de Evaluaciones (Contrato EV-06)
- **Interfaz `IEvaluacionesClient`** (`PEI.IA.Application.Interfaces`): contrato para persistir la calificación sugerida en Evaluaciones.
- **Implementación `EvaluacionesHttpClient`** (`PEI.IA.Infrastructure.Evaluaciones`):
  - Realiza un `POST` a `interno/entregas/{entregaId}/calificacion-sugerida`.
  - **Resiliencia:** Si el servicio de Evaluaciones no responde, está caído o da timeout, la llamada no lanza excepciones, loguea la advertencia y permite que el flujo de corrección continúe retornando la sugerencia al docente.
  - **Modo Stub (`HabilitarIntegracion: false`):** Permite ejecutar y probar el servicio localmente sin requerir que el endpoint `S2-EV-02` esté corriendo.

### 2. Actualización de la Tool MCP `RegistrarCalificacionSugerida`
- Nuevo parámetro obligatorio **`entregaId`** (`Guid`), asociando unívocamente la corrección a la entrega evaluada.
- Nuevo parámetro obligatorio **`puntajeMaximo`** (`decimal`), utilizado para validación de rango:
  - Error pedagógico si `notaSugerida < 0`.
  - Error si `puntajeMaximo <= 0`.
  - Error si `notaSugerida > puntajeMaximo`.
- La entidad `CalificacionSugerida` en dominio incluye la propiedad `EntregaId`.

### 3. Exposición de Recursos MCP: `RubricaResource`
- Se expone el recurso MCP bajo el template de URI **`rubrica://{evaluacionId}`**.
- Permite que los modelos de lenguaje soliciten y carguen la rúbrica estructurada como contexto adicional previo o durante la evaluación.
- Integrado en `PEI.IA.McpServer/Program.cs` mediante los handlers de recursos MCP (`WithListResourceTemplatesHandler` y `WithReadResourceHandler`).

### 4. Orquestación en `CorreccionService`
- Al recibir `CorregirRespuestaRequest` con `EntregaId`:
  1. Invoca al LLM con el system prompt y la rúbrica.
  2. Ejecuta la tool MCP `RegistrarCalificacionSugerida` (si el LLM realiza tool calling) o parsea la respuesta JSON.
  3. Notifica automáticamente a Evaluaciones (`_evaluacionesClient.RegistrarSugerenciaAsync`) con la nota sugerida, justificación y desglose de criterios.
  4. Retorna la respuesta completa al docente.

---

## ⚙️ Configuración (`appsettings.json`)

```json
{
  "EvaluacionesSettings": {
    "BaseUrl": "http://localhost:5103",
    "TimeoutMs": 10000,
    "HabilitarIntegracion": false
  },
  "McpSettings": {
    "McpServerUrl": "http://localhost:5106/mcp",
    "UsarPlanB": true
  }
}
```

---

## 🧪 Pruebas Unitarias

Para ejecutar la suite completa de pruebas unitarias:

```bash
dotnet test services/ia/tests/PEI.IA.UnitTests/PEI.IA.UnitTests.csproj
```

Pruebas incluidas:
- `RegistrarCalificacionToolTests`: validaciones de rango, nota negativa, puntaje máximo, persistencia de `EntregaId`.
- `EvaluacionesHttpClientTests`: pruebas de integración HTTP mockeadas (200 OK, 500 error, conexión rechazada, timeout y modo stub).
- `McpToolExecutorTests` & `PromptBuilderTests`: orquestación de LLM, tool calling y construcción de prompts pedagógicos.
