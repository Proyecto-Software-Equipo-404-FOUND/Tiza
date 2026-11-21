# ====================================================================
# stop-all.ps1 - Detiene todos los servicios locales de Tiza
# ====================================================================
Write-Host "Deteniendo servicios locales de Tiza..." -ForegroundColor Yellow

$ports = @(5293, 5102, 5103, 5104, 5106, 5105, 8080, 3000)
$killed = 0

foreach ($port in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($conns) {
        $pids = $conns.OwningProcess | Select-Object -Unique
        foreach ($pidToKill in $pids) {
            $proc = Get-Process -Id $pidToKill -ErrorAction SilentlyContinue
            if ($proc) {
                Write-Host "Deteniendo proceso $($proc.ProcessName) (PID: $pidToKill, Puerto: $port)..." -ForegroundColor DarkYellow
                Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
                $killed++
            }
        }
    }
}

Write-Host "Servicios detenidos ($killed procesos finalizados)." -ForegroundColor Green
Write-Host "Nota: Los contenedores de base de datos siguen corriendo. Para detenerlos: docker compose -f .\infra\docker-compose.yml down" -ForegroundColor DarkGray

