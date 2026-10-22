# Dataset de Corrección IA — v1

## Propósito
Conjunto de datos ficticio para evaluar la precisión del modelo de IA en la corrección automatizada de respuestas abiertas basadas en rúbrica.

## Estructura
- **3 preguntas abiertas** sobre temas de programación:
  1. **Herencia en POO** (puntaje máximo: 10)
  2. **Normalización en BD** (puntaje máximo: 10)
  3. **Patrón MVC** (puntaje máximo: 10)
- **10 respuestas por pregunta** (total: 30 respuestas)
- Cada respuesta incluye:
  - `texto`: la respuesta del alumno
  - `notaEsperada`: la calificación que un docente daría (gold standard)
  - `justificacionEsperada`: explicación de la nota

## Formato
Archivo JSON (`dataset-correccion-v1.json`) con la estructura:
```json
{
  "version": 1,
  "preguntas": [
    {
      "id": "uuid",
      "enunciado": "...",
      "tipo": "Abierta",
      "puntajeMaximo": 10,
      "rubrica": { "criterios": [...] },
      "respuestas": [
        {
          "id": "...",
          "texto": "...",
          "notaEsperada": 8,
          "justificacionEsperada": "..."
        }
      ]
    }
  ]
}
```

## Distribución de notas
Cada pregunta cubre el espectro completo:
- Respuestas perfectas (10/10)
- Respuestas parciales (4-8/10)
- Respuestas incorrectas o fuera de tema (0-2/10)

## Uso
- **IA2-04**: ejecutar corrección sobre las 30 respuestas y comparar `notaSugerida` vs `notaEsperada`
- **IA1-03**: inyectar la rúbrica como contexto al LLM
- **PO-05**: validar con docente piloto la calidad de las correcciones
