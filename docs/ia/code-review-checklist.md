# 📋 Checklist de Code Review Cruzado — Frente IA (Sprint 1)

> **Módulo:** Microservicio de Inteligencia Artificial (`services/ia`)  
> **Revisión Cruzada:** Desarrollador IA-1 (Franco Ávila) ↔ Desarrollador IA-2 (Santiago Bruno)  
> **Objetivo:** Asegurar calidad de código, robustez arquitectónica, seguridad pedagógica y cumplimiento de normativas de privacidad antes del merge a `main`.

---

## 1. Arquitectura y Principios de Diseño
- [ ] **Separación estricta de 4 capas:**
  - `PEI.IA.Domain`: Entidades puras (`SolicitudCorreccion`, `CalificacionSugerida`) sin dependencias de infraestructura ni frameworks web.
  - `PEI.IA.Application`: DTOs, interfaces (`ILlmClient`, `IPromptBuilder`, `IToolExecutor`), servicios de orquestación (`CorreccionService`) y lógica de dataset.
  - `PEI.IA.Infrastructure`: Clientes externos (`OpenAiCompatibleClient`), integración MCP (`McpToolExecutor`, `LocalToolExecutor`) y settings.
  - `PEI.IA.Api`: Controladores minimalistas, configuración de DI, middlewares y Swagger.
- [ ] **Inyección de Dependencias:** Todos los servicios e interfaces (`ILlmClient`, `IToolExecutor`, `IPromptBuilder`, `ICorreccionService`) están registrados en el contenedor de IoC de ASP.NET Core con los ciclos de vida adecuados (`Singleton` o `Scoped`).
- [ ] **Integración con BuildingBlocks:**
  - Uso de `CorrelationIdMiddleware` para trazabilidad de peticiones distribuidas.
  - Uso de `ProblemDetailsMiddleware` conforme a la RFC 9457 para respuestas de error estandarizadas.

---

## 2. Integración LLM y Tool Calling (MCP)
- [ ] **Abstracción del Proveedor:** El cliente LLM (`ILlmClient` / `OpenAiCompatibleClient`) es agnóstico del proveedor concreto (Groq, OpenRouter, vLLM local).
- [ ] **Manejo de Tool Calling:**
  - El flujo soporta el ciclo de tool calling automático (`tools` y `tool_choice: auto`).
  - El resultado de la tool se procesa y mapea fielmente al DTO de salida `CorregirRespuestaResponse`.
- [ ] **Resiliencia y Plan B:**
  - Si el servidor MCP remoto no está disponible, `McpToolExecutor` conmuta de manera transparente al `LocalToolExecutor` sin arrojar excepciones no controladas ni interrumpir el servicio.
  - Si no hay API key de LLM o el proveedor remoto falla, el sistema provee una degradación elegante con justificación clara.
- [ ] **Telemetría y Métricas:**
  - Se registran tokens de entrada (`TokensEntrada`), tokens de salida (`TokensSalida`) y latencia en milisegundos (`LatenciaMs`).

---

## 3. Seguridad Pedagógica y Anti-Alucinación
- [ ] **Prompt Pedagógico Versionado (`v1_correccion_pedagogica`):**
  - Contiene instrucciones explícitas de anti-alucinación: el modelo no debe otorgar puntaje por conceptos ausentes en la respuesta del alumno.
  - Delimitadores claros entre la consigna, la rúbrica y la respuesta del alumno (`[INICIO_RESPUESTA]` ... `[FIN_RESPUESTA]`) para prevenir prompt injection básico.
- [ ] **Rango de Calificación:** Las notas sugeridas están estrictamente acotadas entre 0.0 y el `PuntajeMaximo` estipulado en la consigna.

---

## 4. Privacidad y Protección de Datos (Ley 25.326)
- [ ] **Ausencia de PII en Prompts:** No se transmiten nombres, DNIs ni identificadores de alumnos al LLM externo.
- [ ] **Cumplimiento de Matriz de Privacidad:** Las solicitudes de prueba en desarrollo emplean únicamente el dataset sintético/ficticio (`IA1-05`).

---

## 5. Pruebas y Calidad de Código
- [ ] **Tests Unitarios:**
  - Todas las pruebas en `tests/PEI.IA.UnitTests` compilan y pasan en verde (`dotnet test`).
  - Cobertura de clientes LLM, orquestación, constructores de prompts, dataset reader y herramientas MCP.
- [ ] **Benchmark Runner:**
  - La herramienta `PEI.IA.BenchmarkRunner` ejecuta de punta a punta las 30 respuestas de prueba y exporta el reporte CSV de métricas (MAE, latencia y costo).
- [ ] **Compilación limpia:** `dotnet build` finaliza con 0 errores y 0 advertencias.
- [ ] **Documentación OpenAPI:**
  - El archivo `contracts/openapi/ia-v1.json` está actualizado y sincronizado con los endpoints del API.
  - Los endpoints y DTOs cuentan con XML comments completos para Swagger UI.
