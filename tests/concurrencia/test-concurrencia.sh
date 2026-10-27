#!/usr/bin/env bash
set -euo pipefail
# Mismo motor que PowerShell: requiere .NET 8 y QA07_CONNECTION.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$script_dir/Runner/QA07.Runner.csproj" -- "$@"
