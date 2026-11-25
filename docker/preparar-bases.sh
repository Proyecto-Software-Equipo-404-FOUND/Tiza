#!/bin/sh
set -e
U="${POSTGRES_USER:-postgres}"
P="${POSTGRES_PASSWORD:-postgres}"

echo "== Identidad: migraciones =="
export ConnectionStrings__IdentidadDb="Host=postgres-identidad;Port=5432;Database=pei_identidad_db;Username=$U;Password=$P"
dotnet ef database update --no-build \
  --project services/identidad/src/PEI.Identidad.Infrastructure \
  --startup-project services/identidad/src/PEI.Identidad.Api --context IdentidadDbContext
dotnet ef database update --no-build \
  --project services/identidad/src/PEI.Identidad.Infrastructure \
  --startup-project services/identidad/src/PEI.Identidad.Api --context PlataformaDbContext

echo "== Cursos: migraciones =="
export ConnectionStrings__CursosDb="Host=postgres-cursos;Port=5432;Database=pei_cursos_db;Username=$U;Password=$P"
dotnet ef database update --no-build \
  --project services/cursos/src/PEI.Cursos.Infrastructure \
  --startup-project services/cursos/src/PEI.Cursos.Api

echo "== Evaluaciones: migraciones =="
export ConnectionStrings__Default="Host=postgres-evaluaciones;Port=5432;Database=pei_evaluaciones_db;Username=$U;Password=$P"
dotnet ef database update --no-build \
  --project services/evaluaciones/src/PEI.Evaluaciones.Infrastructure \
  --startup-project services/evaluaciones/src/PEI.Evaluaciones.Api

if [ "$ID08_ENABLED" != "true" ]; then
  echo "Usuarios semilla: omitidos (para cargarlos, completá .env.semilla; ver docs/DOCKER.md)."
  exit 0
fi

echo "== Identidad: usuarios semilla (ID-08) =="
# El cargador solo acepta el destino de desarrollo localhost:5441/pei_identidad_db.
# Dentro de este contenedor, localhost:5441 se reenvía a la base de Identidad del compose.
socat TCP-LISTEN:5441,fork,reuseaddr TCP:postgres-identidad:5432 &
sleep 1
export ID08_ENVIRONMENT=Development
export ID08_SCHEMA=public
export ID08_ALLOW_DEVELOPMENT_PUBLIC=true
export ID08_OPERATOR="${ID08_OPERATOR:-docker}"
export ID08_CONNECTION="Host=localhost;Port=5441;Database=pei_identidad_db;Username=$U;Password=$P"
export ID08_ALLOWED_DESTINATIONS='[{"Host":"localhost","Port":5441,"Database":"pei_identidad_db","Schema":"public"}]'
dotnet run --no-build --no-launch-profile --project services/identidad/tools/PEI.Identidad.DevelopmentSeed
