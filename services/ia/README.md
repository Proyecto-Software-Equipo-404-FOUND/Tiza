# Microservicio de IA — PEI.IA

Microservicio encargado de la corrección automatizada y pedagógica de respuestas abiertas mediante modelos de lenguaje (LLM), utilizando el protocolo **Model Context Protocol (MCP)** para invocar herramientas pedagógicas y exponer recursos.

---

## 🏛️ Arquitectura del Servicio

El servicio está estructurado siguiendo principios de arquitectura limpia y desacoplamiento por capas:

```
services/ia/
├── src/
│   ├── PEI.IA.Api/              # Host Web API, endpoints REST (/api/v1/correccion), DI, Swagger
│   ├── PEI.IA.Application/      # Casos de uso (CorreccionService), DTOs, interfaces de clientes, prompts y repositorios
│   ├── PEI.IA.Domain/           # Entidades (CalificacionSugerida, SolicitudCorreccion) y reglas de dominio
│   ├── PEI.IA.Infrastructure/  # Clientes HTTP (OpenAiCompatibleClient, EvaluacionesHttpClient, PollyResilientLlmClient, McpToolExecutor) y repositorios en memoria
│   └── PEI.IA.McpServer/        # Servidor MCP standalone (Tools y Recursos MCP)
├── datasets/                    # Datasets de evaluación y pruebas (dataset-correccion-v1.json)
├── prompts/                     # Plantillas de prompts pedagógicos (v1_correccion_pedagogica.md)
└── tests/
    └── PEI.IA.UnitTests/        # Pruebas unitarias de tools, servicios, resiliencia Polly y auditoría
```

---

## 🚀 Cambios Sprint 2

### Tarea S2-IA-01 — Integración MCP y Evaluaciones
1. **Integración con Microservicio de Evaluaciones (Contrato EV-06):**
   - Interfaz `IEvaluacionesClient` e implementación `EvaluacionesHttpClient`.
   - Endpoint: `POST interno/entregas/{entregaId}/calificacion-sugerida`.
   - Resiliencia ante fallos HTTP y flag `HabilitarIntegracion` (modo stub local).
2. **Actualización de Tool MCP `RegistrarCalificacionSugerida`:**
   - Parámetros `entregaId` y `puntajeMaximo` con validaciones de rango.
3. **Exposición de Recursos MCP (`RubricaResource`):**
   - URI Template: `rubrica://{evaluacionId}`.
4. **Orquestación en `CorreccionService`:**
   - Envío automático de la calificación a Evaluaciones si `EntregaId` está presente.

### Tarea S2-IA-02 — Conmutación automática de proveedores IA (Polly Circuit Breaker)
1. **Configuración Multi-Proveedor (`LlmSettings.Providers`):**
   - Soporte para lista priorizada de proveedores (ej. Groq como principal, OpenRouter/Gemini como fallback 1, Local/Ollama como fallback 2).
   - Retrocompatible con la configuración de proveedor único en la raíz de `LlmSettings`.
2. **Cliente Resiliente (`PollyResilientLlmClient`):**
   - Utiliza **Polly v8** con políticas de Circuit Breaker individuales por proveedor.
   - Detecta fallos conmutables: HTTP 429 (*Too Many Requests*), errores HTTP 5xx, timeouts y desconexiones de red.
   - Si un proveedor falla, el cliente conmuta de inmediato y de forma transparente al siguiente proveedor según su prioridad.
   - Si un proveedor acumula fallos consecutivos (`FallosConsecutivosParaAbrirCircuito`), su Circuit Breaker se abre (`BrokenCircuitException`), evitando llamadas innecesarias y saltando de inmediato a proveedores alternativos saludables.
3. **Auditoría de Solicitudes IA (`SolicitudCorreccion` / `ISolicitudCorreccionRepository`):**
   - Cada ciclo de corrección registra la entidad de dominio `SolicitudCorreccion`.
   - Almacena: `InstitucionId`, `EvaluacionId`, `PreguntaId`, `Proveedor` real que respondió (Groq, OpenRouter, etc.), `Modelo`, tokens de entrada (`TokensEntrada`), tokens de salida (`TokensSalida`), latencia (`LatenciaMs`), nota sugerida y estado (`Completada` / `Fallida`).
   - Implementación thread-safe en memoria (`InMemorySolicitudCorreccionRepository`) con soporte de filtrado por institución.

---

## ⚙️ Configuración (`appsettings.json`)

```json
{
  "LlmSettings": {
    "Provider": "Groq",
    "ApiKey": "",
    "BaseUrl": "https://api.groq.com/openai/v1",
    "Model": "llama-3.3-70b-versatile",
    "Temperature": 0.1,
    "MaxTokens": 2048,
    "TimeoutMs": 15000,
    "Providers": [
      {
        "Provider": "Groq",
        "ApiKey": "",
        "BaseUrl": "https://api.groq.com/openai/v1",
        "Model": "llama-3.3-70b-versatile",
        "Temperature": 0.1,
        "MaxTokens": 2048,
        "TimeoutMs": 15000,
        "Prioridad": 1
      },
      {
        "Provider": "OpenRouter",
        "ApiKey": "",
        "BaseUrl": "https://openrouter.ai/api/v1",
        "Model": "google/gemini-2.0-flash-exp:free",
        "Temperature": 0.1,
        "MaxTokens": 2048,
        "TimeoutMs": 15000,
        "Prioridad": 2
      },
      {
        "Provider": "Local",
        "ApiKey": "",
        "BaseUrl": "http://localhost:11434/v1",
        "Model": "llama3",
        "Temperature": 0.1,
        "MaxTokens": 2048,
        "TimeoutMs": 10000,
        "Prioridad": 3
      }
    ]
  },
  "LlmResilienceOptions": {
    "FallosConsecutivosParaAbrirCircuito": 2,
    "DuracionAperturaCircuitoSegundos": 30
  },
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

Pruebas cubiertas (41 tests):
- `PollyResilientLlmClientTests`: pruebas de 200 OK, conmutación ante error 429 Too Many Requests, timeout, fallo de todos los proveedores y apertura de Circuit Breaker.
- `CorreccionServiceAuditoriaTests`: auditoría completa de métricas de `SolicitudCorreccion`, estado de fallo y filtrado por institución.
- `RegistrarCalificacionToolTests`: validaciones de rango, nota negativa, puntaje máximo, persistencia de `EntregaId`.
- `EvaluacionesHttpClientTests`: integración HTTP con Evaluaciones mockeada (200 OK, 500 error, desconexión, timeout, modo stub).
- `McpToolExecutorTests` & `PromptBuilderTests`: orquestación de LLM, tool calling y plantillas pedagógicas.
