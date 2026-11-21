# S3-CU-04 — Una matrícula activa por curso

El alta toma el curso de la cohorte y la institución del contexto autenticado.
Una baja no impide el alta en otra cohorte. Para volver a la misma cohorte se usa
la reactivación; se conserva la unicidad histórica por cohorte y alumno.
La segunda matrícula activa entre cohortes se rechaza con 409 ProblemDetails
y type ALUMNO_EN_OTRA_COMISION, tanto en el alta como en la reactivación.

Cuando se conoce la comisión donde el alumno ya está activo, el 409 suma
`cohorteActualId` y `cohorteActualNombre` (contrato `cursos-v1.json`, acordado
en la PR #253), para que el administrador sepa dónde darlo de baja. En un choque
simultáneo detectado por el índice de la base, el 409 sale sin esos dos campos.
La colección de Cursos agrega un escenario optativo, ejecutable al definir
cu04CohorteDestinoId en QA.

## Cambio estructural en las tablas

Migración `20261118023453_UnaMatriculaActivaPorCurso`:

- **Columna nueva `CursoId` en `Matriculaciones`** (obligatoria). La migración la
  completa con el curso de la cohorte de cada matrícula existente. En `Domain`,
  `Matriculacion` se construye ahora con
  `new Matriculacion(institucionId, cohorteId, alumnoId, cursoId)`.
- **Clave alternativa en `Cohortes`**: `AK_Cohortes_InstitucionId_CursoId_Id`
  sobre `(InstitucionId, CursoId, Id)`, necesaria para la FK compuesta.
- **FK compuesta** `FK_Matriculaciones_Cohortes_InstitucionId_CursoId_CohorteId`:
  `(InstitucionId, CursoId, CohorteId)` de `Matriculaciones` apunta a `Cohortes`.
  Reemplaza la FK simple por `CohorteId` y garantiza que la matrícula tenga la
  misma institución y el mismo curso que su cohorte.
- **Índice único parcial** `UX_Matriculaciones_Activa_Institucion_Curso_Alumno`
  sobre `(InstitucionId, CursoId, AlumnoId)` con filtro `"Estado" = 'Activa'`:
  la base no deja dos matrículas activas del mismo alumno en el mismo curso, aunque
  lleguen dos altas al mismo tiempo. Las bajas no cuentan.
- Se conserva la unicidad histórica por cohorte y alumno (una fila por par).

**Antes de migrar una base con datos:** la migración se detiene con error si
encuentra matrículas con institución, curso o cohorte inconsistentes, o un alumno
con más de una matrícula activa en el mismo curso. En ese caso hay que dar de baja
las matrículas sobrantes y volver a correr la migración.

## Verificación en PostgreSQL

Usar exclusivamente un clúster nuevo y desechable, escuchando en 127.0.0.1,
en un puerto libre distinto de 5432, 5441–5445 y 5541, con base
pei_cursos_cu04_tests y usuario cu04_test. Usar una contraseña efímera externa.
Asignar su cadena a TIZA_CU04_TEST_CONNECTION y ejecutar
`dotnet test services/cursos/PEI.Cursos.sln`. Sin la variable, los casos con
PostgreSQL se omiten. Los datos y los esquemas aleatorios se crean solo en ese
destino; no ejecutar esta suite apuntando a una instancia de desarrollo.

Los casos sincronizan ambas escrituras antes de SaveChanges, después de superar
la validación preventiva. Cubren alta/alta, alta/reactivación y
reactivación/reactivación, además de migración y HTTP con JWT y persistencia
real. El arranque del host de pruebas utiliza Testing sin el seeder de Development.

### Ejecución reproducible en PowerShell con PostgreSQL local

Requiere .NET 8 y los binarios `initdb`, `pg_ctl` y `createdb` de PostgreSQL
instalados y disponibles en `PATH`. Ejecutar desde la raíz de este worktree.
Ejecutar el bloque directamente, sin canalizar su salida a otros cmdlets: en
Windows, los procesos hijos de `pg_ctl` pueden mantener abierta esa canalización.
No utiliza Docker, servicios de Windows ni las bases existentes: inicializa
un clúster independiente en un directorio temporal nuevo y usa un puerto libre.
La contraseña se genera para esta ejecución y no debe copiarse a Git.

```powershell
$ErrorActionPreference = 'Stop'
$clusterCu04 = Join-Path ([IO.Path]::GetTempPath()) ('tiza-cu04-' + [guid]::NewGuid().ToString('N'))
$dataCu04 = Join-Path $clusterCu04 'data'
$passwordFileCu04 = Join-Path $clusterCu04 'password.tmp'
$previousConnectionCu04 = $env:TIZA_CU04_TEST_CONNECTION
$previousPasswordCu04 = $env:PGPASSWORD
$startedCu04 = $false
function Assert-Cu04Exit {
    if ($LASTEXITCODE -ne 0) { throw "El comando anterior falló: $LASTEXITCODE" }
}
try {
    New-Item -ItemType Directory -Path $clusterCu04 | Out-Null
    do {
        $listenerCu04 = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listenerCu04.Start()
        $portCu04 = $listenerCu04.LocalEndpoint.Port
        $listenerCu04.Stop()
    } while ($portCu04 -in @(5432, 5441, 5442, 5443, 5444, 5445, 5541))
    $passwordCu04 = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
    [IO.File]::WriteAllText($passwordFileCu04, $passwordCu04, [Text.UTF8Encoding]::new($false))
    & initdb -D $dataCu04 -U cu04_test --auth=scram-sha-256 --encoding=UTF8 --no-locale --pwfile=$passwordFileCu04
    Assert-Cu04Exit
    Remove-Item -LiteralPath $passwordFileCu04
    & pg_ctl -D $dataCu04 -l (Join-Path $clusterCu04 'postgres.log') -o "-h 127.0.0.1 -p $portCu04" -w start
    Assert-Cu04Exit
    $startedCu04 = $true
    $env:PGPASSWORD = $passwordCu04
    & createdb -h 127.0.0.1 -p $portCu04 -U cu04_test pei_cursos_cu04_tests
    Assert-Cu04Exit
    $env:TIZA_CU04_TEST_CONNECTION = "Host=127.0.0.1;Port=$portCu04;Database=pei_cursos_cu04_tests;Username=cu04_test;Password=$passwordCu04;Pooling=false"

    & dotnet test services/cursos/PEI.Cursos.sln --logger 'console;verbosity=normal'
    Assert-Cu04Exit
    & dotnet build services/cursos/PEI.Cursos.sln --no-restore
    Assert-Cu04Exit
} finally {
    $env:TIZA_CU04_TEST_CONNECTION = $previousConnectionCu04
    $env:PGPASSWORD = $previousPasswordCu04
    if (Test-Path -LiteralPath $passwordFileCu04) { Remove-Item -LiteralPath $passwordFileCu04 }
    if ($startedCu04) {
        & pg_ctl -D $dataCu04 -m fast -w stop
        Assert-Cu04Exit
    }
    $passwordCu04 = $null
    Write-Host "Clúster temporal (para revisar y eliminar manualmente tras confirmar su detención): $clusterCu04"
}
```

Verificar en el resultado que los **nueve casos PostgreSQL** de
`MatriculaActivaPostgresTests` se ejecuten, con cero omisiones. El caso
`DosAltasSimultaneasEnLaMismaCohorteConservanElConflictoHistorico` envía dos
solicitudes HTTP con JWT y sincroniza ambos DbContext antes de guardar:
comprueba un 201, un 409 histórico sin código entre comisiones y una única
matrícula persistida. Un fallo de la barrera hace fallar la prueba.

Las migraciones se aplican solamente en esquemas aleatorios del clúster temporal;
cada prueba elimina su esquema al terminar. El script detiene ese clúster en
`finally` y conserva sus archivos para inspección; no modificar ni eliminar
ningún directorio de una instalación existente. Las variables y la contraseña
pueden permanecer en copias de memoria durante la sesión; limpiarlas no garantiza
borrar todas esas copias. No imprimir la cadena de conexión.

## Aplicación futura de la migración

Ejecutar con las escrituras de Cursos detenidas: el backfill y la construcción
de índices adquieren locks. Si se detectan relaciones incoherentes o varias
activas, se revierte la migración; resolver esas filas mediante una decisión
operativa antes de reintentar. La migración no elige ganadores ni borra historial.
