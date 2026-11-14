$Host.UI.RawUI.WindowTitle = "7 - Gateway (8080)"
$env:PATH = "C:\Program Files (x86)\dotnet;$env:PATH"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Gateway en http://localhost:8080..." -ForegroundColor Cyan
dotnet run --project gateway/src/PEI.Gateway --launch-profile http

