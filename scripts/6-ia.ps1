$Host.UI.RawUI.WindowTitle = "6 - IA OpenRouter (5105)"
$env:PATH = "C:\Program Files (x86)\dotnet;$env:PATH"
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

Write-Host "Levantando Servicio IA con OpenRouter en http://localhost:5105..." -ForegroundColor Cyan
dotnet run --project services/ia/src/PEI.IA.Api --launch-profile http
