$Host.UI.RawUI.WindowTitle = "8 - Frontend (3000)"
Set-Location "$PSScriptRoot\..\frontend"
Write-Host "Levantando Frontend en http://localhost:3000..." -ForegroundColor Cyan
npm.cmd run dev -- --host

