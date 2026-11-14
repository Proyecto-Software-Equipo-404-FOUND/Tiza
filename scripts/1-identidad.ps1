$Host.UI.RawUI.WindowTitle = "1 - Identidad (5293)"
$env:PATH = "C:\Program Files (x86)\dotnet;$env:PATH"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Identidad en http://localhost:5293..." -ForegroundColor Cyan
dotnet run --project services/identidad/src/PEI.Identidad.Api --launch-profile http

