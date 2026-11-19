$Host.UI.RawUI.WindowTitle = "5 - Servidor MCP de IA (5106)"
Set-Location "$PSScriptRoot\.."
Write-Host "Levantando Servidor MCP en http://localhost:5106..." -ForegroundColor Cyan
dotnet run --project services/ia/src/PEI.IA.McpServer

