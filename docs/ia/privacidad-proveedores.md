# 🛡️ Matriz de Privacidad y Cumplimiento Normativo de Proveedores de IA

> **Frente:** Inteligencia Artificial · Tiza PEI  
> **Tarea:** `IA2-05`  
> **Marco Legal de Referencia:** Ley N° 25.326 de Protección de los Datos Personales (República Argentina) y Disposiciones de la Agencia de Acceso a la Información Pública (AAIP).

---

## 1. Contexto y Objetivos

El microservicio de Inteligencia Artificial de la plataforma **Tiza (PEI)** asiste en la corrección automatizada de evaluaciones y respuestas abiertas redactadas por estudiantes. Debido a que las respuestas procesadas pueden contener datos identificables o inferibles relativos a personas humanas (estudiantes, docentes, instituciones), es imperativo evaluar el tratamiento de la información bajo la legislación argentina de protección de datos personales.

Los principios rectores aplicables (Ley 25.326) son:
1. **Principio de Licitud y Finalidad (Art. 3):** Los datos no pueden utilizarse para finalidades distintas o incompatibles con aquellas para las cuales fueron recolectados (evaluación pedagógica).
2. **Principio de Confidencialidad y Seguridad (Arts. 9 y 10):** Se deben adoptar medidas técnicas para evitar el acceso no autorizado o la pérdida de integridad.
3. **Transferencia Internacional de Datos (Art. 12):** Está prohibida la transferencia a países u organismos que no proporcionen niveles adecuados de protección, salvo consentimientos explícitos o contratos con cláusulas tipo homologadas por la AAIP.

---

## 2. Matriz Comparativa de Proveedores

| Proveedor / Solución | Retención de Datos | Jurisdicción Servidores | Uso de Datos para Entrenamiento | Cumplimiento Ley 25.326 | Nivel de Riesgo | Observaciones / Términos de Servicio |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Groq Cloud (Free Tier)** | 30 días en logs operativos | EE.UU. (California) | **No** (política actual DPA/API) | ⚠️ **Parcial** (Requiere Cláusula DPA / Transferencia Internacional) | **Medio** | Máxima velocidad de inferencia (LPUs). Excelente para prototipado y MVP; en producción requiere anonimización previa en el backend de Tiza. |
| **Groq Cloud (Enterprise / Paid)** | Retención configurable (posibilidad de zero-data-retention) | EE.UU. | **No** | 🟢 **Aceptable** con acuerdo DPA formal | **Bajo - Medio** | Ofrece SLAs de disponibilidad y garantías contractuales comerciales para empresas. |
| **Google Gemini (API Developer)** | Hasta 18 meses para cuentas gratuitas; en API de pago no se utiliza para entrenar | EE.UU. / Global | **No** en planes Cloud Vertex AI / Gemini API corporativa; sí en interfaces web de consumo | ⚠️ **Parcial** | **Medio** | Plataforma robusta con opción de Vertex AI (donde aplica Google Cloud DPA con cláusulas modelo). |
| **OpenRouter / Moonshot** | Varía según el agregador y el modelo de destino (ej. 30 días a persistente) | EE.UU. / Internacional (según endpoint) | Sujeto a cada laboratorio subyacente (ej. Meta, Mistral, Anthropic) | ⚠️ **Bajo cumplimiento directo** | **Alto** | Al ser un intermediario o gateway externo, los datos viajan por múltiples entidades jurídicas antes de llegar al modelo. |
| **Servidor On-Premise / Local (Ariel)** | 0 días / Persistencia local exclusiva en PostgreSQL de Tiza | 🇦🇷 **Argentina** (Infraestructura propia / nodo local) | **No** (pesos congelados, inferencia local) | 🟢 **Cumplimiento Total** | **Mínimo** | No existe transferencia internacional de datos. Los datos de los estudiantes nunca salen de la infraestructura controlada por la institución. |

---

## 3. Análisis Normativo Detallado (Ley 25.326)

### 3.1. Transferencia Internacional de Datos (Artículo 12)
- **Problemática:** La mayoría de los proveedores LLM comerciales (Groq, OpenAI, Google) residen en Estados Unidos. La AAIP no considera a EE.UU. como país con legislación adecuada plena por defecto, a excepción de mecanismos específicos o cláusulas contractuales homologadas.
- **Mitigación en Tiza:**
  - El backend de IA ejecuta una capa de **anonimización/pesudonomización** antes de enviar cualquier texto a un LLM en la nube:
    - Se extirpa el nombre del alumno, DNI, comisión o colegio.
    - Se envía únicamente: `PreguntaEnunciado`, `RubricaJson` y `RespuestaTexto`.
    - No se envían datos biométricos, socioeconómicos ni información sensible (Art. 2 y 7).

### 3.2. Prohibición de Entrenamiento con Datos Académicos
- Ningún proveedor utilizado debe nutrir sus modelos fundacionales con exámenes de alumnos argentinos menores de edad o universitarios sin consentimiento expreso informado de los tutores o titulares.
- Groq Cloud en su API explícitamente garantiza que no entrena modelos con datos enviados vía API.

---

## 4. Recomendaciones Arquitectónicas para Tiza

1. **Sprint 1 (Fase Actual - Prototipo y Pruebas):**
   - Utilizar **Groq Cloud** para el desarrollo ágil por su bajísima latencia (~300-500 ms) y tier gratuito, empleando únicamente el dataset sintético/ficticio creado en `IA1-05` (datos 100% ficticios sin riesgo de privacidad).
2. **Entornos Productivos con Alumnos Reales:**
   - **Opción Primaria:** Servidor Local (nodo local del equipo / vLLM / Ollama con LLaMA 3.3 70B o 8B cuantizado) dentro del perímetro institucional.
   - **Opción Secundaria:** Groq / Azure OpenAI con DPA firmado, precedido obligatoriamente por el middleware de anonimización de tokens de Tiza.
3. **Persistencia de Auditoría:**
   - La tabla `solicitudes_correccion` en `pei_ia_db` almacena únicamente los identificadores de trazabilidad (`SolicitudId`, `InstitucionId`, métricas de costo/latencia), asegurando el derecho de acceso y supresión (Arts. 14 y 16, Ley 25.326).
