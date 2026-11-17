$Host.UI.RawUI.WindowTitle = "3 - Evaluaciones (5103)"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Evaluaciones en http://localhost:5103..." -ForegroundColor Cyan
dotnet run --project services/evaluaciones/src/PEI.Evaluaciones.Api --launch-profile http

