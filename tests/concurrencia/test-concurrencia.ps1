# ==============================================================================
# Prueba de Concurrencia Optimista (PowerShell - Windows)
# ==============================================================================
param(
    [string]$Url = "http://localhost:8080",
    [string]$Id = "00000000-0000-0000-0000-000000000001",
    [string]$Token = "token-de-prueba"
)

Write-Host "Enviando 2 peticiones concurrentes a $Url/api/v1/evaluaciones/$Id..." -ForegroundColor Cyan

$body = @{
    estado = "Publicada"
    version = 1
} | ConvertTo-Json

$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type"  = "application/json"
}

$job1 = Start-Job -ScriptBlock {
    param($u, $h, $b)
    try {
        $res = Invoke-WebRequest -Uri "$u" -Method Patch -Headers $h -Body $b -UseBasicParsing
        return $res.StatusCode
    } catch {
        return $_.Exception.Response.StatusCode.value__
    }
} -ArgumentList "$Url/api/v1/evaluaciones/$Id", $headers, $body

$job2 = Start-Job -ScriptBlock {
    param($u, $h, $b)
    try {
        $res = Invoke-WebRequest -Uri "$u" -Method Patch -Headers $h -Body $b -UseBasicParsing
        return $res.StatusCode
    } catch {
        return $_.Exception.Response.StatusCode.value__
    }
} -ArgumentList "$Url/api/v1/evaluaciones/$Id", $headers, $body

$results = Wait-Job $job1, $job2 | Receive-Job
Write-Host "Resultados HTTP obtenidos: $($results -join ', ')" -ForegroundColor Yellow
Write-Host "Resultado esperado: Uno debe ser 200/204 y el otro 409 (Conflict)." -ForegroundColor Green
