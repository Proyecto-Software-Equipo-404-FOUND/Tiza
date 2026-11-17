$Host.UI.RawUI.WindowTitle = "4 - Sala en Vivo (5104)"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Sala en Vivo en http://localhost:5104..." -ForegroundColor Cyan
dotnet run --project services/sala-en-vivo/src/PEI.SalaEnVivo.Api --launch-profile http

