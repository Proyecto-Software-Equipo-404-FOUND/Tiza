# syntax=docker/dockerfile:1
# Herramienta de una sola vez para bases NUEVAS (por ejemplo, al clonar el repo en otra máquina):
#   1. aplica las migraciones de Identidad (sus dos contextos), Cursos y Evaluaciones
#      (Sala en Vivo se migra sola al arrancar; IA no usa base);
#   2. si se habilita con .env.semilla, carga los usuarios semilla de ID-08 (admin@a.test, docente@a.test, ...).
# Es idempotente: sobre bases que ya tienen todo aplicado no cambia nada.
#   docker compose -f infra/docker-compose.yml run --rm preparar-bases
FROM mcr.microsoft.com/dotnet/sdk:8.0
RUN apt-get update && apt-get install -y --no-install-recommends socat && rm -rf /var/lib/apt/lists/*
RUN dotnet tool install --global dotnet-ef --version 8.0.11
ENV PATH="$PATH:/root/.dotnet/tools" \
    ASPNETCORE_ENVIRONMENT=Development \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet build services/identidad/src/PEI.Identidad.Api && \
    dotnet build services/identidad/tools/PEI.Identidad.DevelopmentSeed && \
    dotnet build services/cursos/src/PEI.Cursos.Api && \
    dotnet build services/evaluaciones/src/PEI.Evaluaciones.Api
COPY docker/preparar-bases.sh /preparar-bases.sh
RUN sed -i 's/\r$//' /preparar-bases.sh && chmod +x /preparar-bases.sh
ENTRYPOINT ["/preparar-bases.sh"]
