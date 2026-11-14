param (
    [switch]$SeparateWindows = $false
)

$repoRoot = $PSScriptRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Levantando entorno local de Tiza     " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Asegurar bases de datos en Docker
Write-Host "`n[1/3] Verificando contenedores de base de datos..." -ForegroundColor Yellow
docker compose -f "$repoRoot\infra\docker-compose.yml" up -d

# 2. Servicios
$scripts = @(
    @{ Name = "1-Identidad"; File = "$repoRoot\scripts\1-identidad.ps1" },
    @{ Name = "2-Cursos"; File = "$repoRoot\scripts\2-cursos.ps1" },
    @{ Name = "3-Evaluaciones"; File = "$repoRoot\scripts\3-evaluaciones.ps1" },
    @{ Name = "4-SalaEnVivo"; File = "$repoRoot\scripts\4-sala-en-vivo.ps1" },
    @{ Name = "5-MCP-IA"; File = "$repoRoot\scripts\5-mcp-ia.ps1" },
    @{ Name = "6-IA"; File = "$repoRoot\scripts\6-ia.ps1" },
    @{ Name = "7-Gateway"; File = "$repoRoot\scripts\7-gateway.ps1" },
    @{ Name = "8-Frontend"; File = "$repoRoot\scripts\8-frontend.ps1" }
)

Write-Host "`n[2/3] Levantando servicios..." -ForegroundColor Yellow

$hasWt = Get-Command wt.exe -ErrorAction SilentlyContinue

if ($hasWt -and -not $SeparateWindows) {
    Write-Host "Abriendo en pestañas dentro de Windows Terminal..." -ForegroundColor Green
    $wtList = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $scripts.Count; $i++) {
        $s = $scripts[$i]
        if ($i -gt 0) {
            $wtList.Add(";")
            $wtList.Add("new-tab")
        }
        $wtList.Add("-d")
        $wtList.Add($repoRoot)
        $wtList.Add("--title")
        $wtList.Add($s.Name)
        $wtList.Add("powershell.exe")
        $wtList.Add("-NoExit")
        $wtList.Add("-ExecutionPolicy")
        $wtList.Add("Bypass")
        $wtList.Add("-File")
        $wtList.Add($s.File)
    }
    Start-Process wt.exe -ArgumentList $wtList.ToArray()
} else {
    Write-Host "Abriendo en ventanas individuales de PowerShell..." -ForegroundColor Green
    foreach ($s in $scripts) {
        Write-Host " -> Iniciando $($s.Name)..."
        Start-Process powershell.exe -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-File", $s.File
        Start-Sleep -Seconds 1
    }
}

Write-Host "`n[3/3] ¡Servicios iniciados!" -ForegroundColor Green
Write-Host "--------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Frontend:  http://localhost:3000" -ForegroundColor Yellow
Write-Host "Gateway:   http://localhost:8080" -ForegroundColor Yellow
Write-Host "Swagger Identidad:    http://localhost:5293/swagger"
Write-Host "Swagger Cursos:       http://localhost:5102/swagger"
Write-Host "Swagger Evaluaciones: http://localhost:5103/swagger"
Write-Host "Swagger SalaEnVivo:   http://localhost:5104/swagger"
Write-Host "--------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Para detener todos los servicios en cualquier momento, corre: .\stop-all.ps1`n" -ForegroundColor DarkGray
