#!/bin/sh
# Arranque común de los servicios .NET.
# Las claves de IA llegan como variables con nombre simple (desde .env.gateway / .env.openrouter)
# y acá se pasan al formato de configuración de .NET, igual que hace scripts/6-ia.ps1.
set -e

if [ -n "$OPENROUTER_API_KEY" ]; then
  export LlmSettings__ApiKey="$OPENROUTER_API_KEY"
  export LlmSettings__Providers__0__ApiKey="$OPENROUTER_API_KEY"
  export LlmSettings__Providers__1__ApiKey="$OPENROUTER_API_KEY"
fi

# Índice 3 = GatewayUnaj (gpt-4.1-mini) en appsettings.json del servicio IA
if [ -n "$UNAJ_GATEWAY_API_KEY" ]; then
  export LlmSettings__Providers__3__ApiKey="$UNAJ_GATEWAY_API_KEY"
fi

# No quedan en el entorno con el nombre original
unset OPENROUTER_API_KEY UNAJ_GATEWAY_API_KEY

exec dotnet "/app/$APP_DLL"
