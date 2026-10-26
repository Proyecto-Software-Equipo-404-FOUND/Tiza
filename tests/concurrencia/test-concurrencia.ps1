param(
    [string]$Url = 'http://127.0.0.1:5103',
    [string]$Database = 'pei_evaluaciones_qa07',
    [string]$Institution = '70707070-7070-4070-8070-707070707070',
    [string]$Cohort = '33333333-3333-3333-3333-333333333333',
    [int]$Repetitions = 10,
    [string]$Id,
    [string]$Output = 'TestResults/qa07-http.json'
)
$ErrorActionPreference = 'Stop'
$runnerArgs = @('--url', $Url, '--database', $Database, '--institution', $Institution,
    '--cohort', $Cohort, '--repetitions', "$Repetitions", '--output', $Output)
if ($Id) { $runnerArgs += @('--id', $Id) }
dotnet run --project "$PSScriptRoot/Runner/QA07.Runner.csproj" -- @runnerArgs
exit $LASTEXITCODE
