# syntax=docker/dockerfile:1
# Imagen genérica para los servicios .NET de Tiza (Identidad, Cursos, Evaluaciones, Sala, IA, MCP, Gateway).
# Se construye desde la raíz del repo: los proyectos referencian building-blocks/ y contracts/.
#   PROYECTO: ruta al .csproj de la API
#   APP_DLL:  nombre del .dll que arranca
#   DATOS:    (opcional) carpeta que se copia a /app/datasets (el MCP lee la rúbrica de ahí)

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROYECTO
ARG DATOS=""
WORKDIR /src
COPY . .
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "$PROYECTO" -c Release -o /out /p:UseAppHost=false
RUN mkdir -p /out/datasets && if [ -n "$DATOS" ]; then cp -r "$DATOS"/. /out/datasets/; fi

FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG APP_DLL
# Development a propósito: es la configuración con la que corre la demo (appsettings.Development.json,
# migraciones y datos semilla al iniciar, Swagger). No es una configuración de producción.
ENV APP_DLL=$APP_DLL \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Development
WORKDIR /app
COPY --from=build /out .
COPY docker/entrypoint.sh /entrypoint.sh
RUN sed -i 's/\r$//' /entrypoint.sh && chmod +x /entrypoint.sh
EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
