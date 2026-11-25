# Levanta TODO Tiza en Docker: bases, Redis, los 7 servicios .NET, el gateway y el frontend.
# Usa las mismas bases y volúmenes que start-all.ps1, así que los datos se conservan.
# No corras a la vez start-all.ps1 (los puertos 3000, 8080 y 5102-5106 se pisarían).
#   .\start-docker.ps1            -> construye lo que cambió y levanta
#   .\start-docker.ps1 -SinBuild  -> levanta con las imágenes que ya están
param([switch]$SinBuild)
$ErrorActionPreference = 'Stop'
$compose = Join-Path $PSScriptRoot 'infra\docker-compose.yml'

Write-Host '== Tiza en Docker ==' -ForegroundColor Cyan
if (-not (Test-Path (Join-Path $PSScriptRoot '.env.gateway'))) {
    Write-Host 'Aviso: no hay .env.gateway; la IA va a usar solo los proveedores gratuitos.' -ForegroundColor Yellow
}

$argumentos = @('compose', '-f', $compose, '--profile', 'app', 'up', '-d')
if (-not $SinBuild) { $argumentos += '--build' }
& docker @argumentos
if ($LASTEXITCODE -ne 0) { throw 'docker compose falló. Revisá el mensaje de arriba.' }

$ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.InterfaceAlias -notmatch 'vEthernet|WSL|Loopback' } |
    Select-Object -First 1).IPAddress

Write-Host ''
Write-Host 'Listo. Esperá unos segundos a que arranquen los servicios.' -ForegroundColor Green
Write-Host '  Tiza:    http://localhost:3000'
if ($ip) { Write-Host "  Celular: http://${ip}:3000 (misma red Wi-Fi)" }
Write-Host '  Gateway: http://localhost:8080'
Write-Host ''
Write-Host 'Ver logs:  docker compose -f infra\docker-compose.yml --profile app logs -f <servicio>'
Write-Host 'Apagar:    .\stop-docker.ps1'
