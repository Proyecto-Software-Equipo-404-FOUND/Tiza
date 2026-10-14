#!/bin/bash
# ==============================================================================
# Prueba de Concurrencia Optimista (Criterio A07 y EV-04)
# Dos peticiones idénticas enviadas en simultáneo modificando la misma versión.
# Resultado esperado: Una petición responde 200/204 y la otra 409 Conflict.
# ==============================================================================

URL="${GATEWAY_URL:-http://localhost:8080}"
ID="${EVALUACION_ID:-00000000-0000-0000-0000-000000000001}"
TOKEN="${AUTH_TOKEN:-token-de-prueba}"

echo "Enviando 2 peticiones concurrentes a $URL/api/v1/evaluaciones/$ID..."

for i in 1 2; do
  curl -s -o /dev/null -w "Respuesta request $i: %{http_code}\n" -X PATCH \
    "$URL/api/v1/evaluaciones/$ID" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"estado":"Publicada","version":1}' &
done

wait
echo "Prueba finalizada. Debería observarse un código 200 y un código 409."
