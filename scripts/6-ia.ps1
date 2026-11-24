$Host.UI.RawUI.WindowTitle = "6 - IA OpenRouter (5105)"
Set-Location "$PSScriptRoot\.."

# Leer clave desde variable de entorno o desde archivo local .env.openrouter (ignorado en git)
$clave = $env:OPENROUTER_API_KEY
if (-not $clave -and (Test-Path "$PSScriptRoot\..\.env.openrouter")) {
    $lines = Get-Content "$PSScriptRoot\..\.env.openrouter"
    foreach ($line in $lines) {
        if ($line -match "^\s*OPENROUTER_API_KEY\s*=\s*(.*)$") {
            $clave = $matches[1].Trim(' "''')
        }
    }
}

if (-not $clave) {
    Write-Host "AVISO: No se encontro OPENROUTER_API_KEY en variables de entorno ni en .env.openrouter" -ForegroundColor Yellow
}

$env:LlmSettings__Providers__0__Provider = "OpenRouter"
$env:LlmSettings__Providers__0__BaseUrl = "https://openrouter.ai/api/v1"
$env:LlmSettings__Providers__0__Model = "nvidia/nemotron-3-ultra-550b-a55b:free"
$env:LlmSettings__Providers__0__TimeoutMs = "25000"

if ($clave) {
    $env:LlmSettings__Providers__0__ApiKey = $clave
    $env:LlmSettings__Providers__1__ApiKey = $clave
    $env:LlmSettings__ApiKey = $clave
}

# S3-IA-03: key del gateway del profesor (gpt-4.1-mini). NUNCA va al repo.
# Se lee de UNAJ_GATEWAY_API_KEY o del archivo local .env.gateway (ignorado en git por la regla .env.*).
# Sin key, el proveedor GatewayUnaj se omite y se usan los gratis de OpenRouter.
$claveGateway = $env:UNAJ_GATEWAY_API_KEY
if (-not $claveGateway -and (Test-Path "$PSScriptRoot\..\.env.gateway")) {
    foreach ($line in Get-Content "$PSScriptRoot\..\.env.gateway") {
        if ($line -match "^\s*UNAJ_GATEWAY_API_KEY\s*=\s*(.*)$") {
            $claveGateway = $matches[1].Trim(' "''')
        }
    }
}
if ($claveGateway) {
    # Indice 3 = GatewayUnaj en appsettings.json (prioridad 1)
    $env:LlmSettings__Providers__3__ApiKey = $claveGateway
    Write-Host "Gateway UNAJ (gpt-4.1-mini) habilitado como proveedor principal." -ForegroundColor Green
} else {
    Write-Host "Sin UNAJ_GATEWAY_API_KEY: se usan los modelos gratis de OpenRouter." -ForegroundColor Yellow
}

Write-Host "Levantando Servicio IA con OpenRouter en http://localhost:5105..." -ForegroundColor Cyan
dotnet run --project services/ia/src/PEI.IA.Api --launch-profile http
