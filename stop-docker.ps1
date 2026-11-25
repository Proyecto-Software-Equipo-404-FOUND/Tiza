# Apaga los contenedores de la aplicación y las bases. Los datos quedan en los volúmenes.
$compose = Join-Path $PSScriptRoot 'infra\docker-compose.yml'
docker compose -f $compose --profile app down
Write-Host 'Tiza apagado. Los datos se conservan.' -ForegroundColor Green
