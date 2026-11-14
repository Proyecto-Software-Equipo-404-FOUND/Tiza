$Host.UI.RawUI.WindowTitle = "3 - Evaluaciones (5103)"
$env:PATH = "C:\Program Files (x86)\dotnet;$env:PATH"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Evaluaciones en http://localhost:5103..." -ForegroundColor Cyan
dotnet run --project services/evaluaciones/src/PEI.Evaluaciones.Api --launch-profile http

