$Host.UI.RawUI.WindowTitle = "2 - Cursos (5102)"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Cursos en http://localhost:5102..." -ForegroundColor Cyan
dotnet run --project services/cursos/src/PEI.Cursos.Api --launch-profile http

