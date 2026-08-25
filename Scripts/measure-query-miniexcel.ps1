param(
    [int[]]$Quantities = @(10000, 50000, 100000),
    [int]$Repetitions = 3,
    [bool]$WarmUp = $true,
    [bool]$ForceGc = $true
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "OutOfMemoryWorkbook.csproj"
$outputDirectory = Join-Path $projectRoot "Resultados"
$dllPath = Join-Path $projectRoot "bin\Release\net10.0\OutOfMemoryWorkbook.dll"
$scenarios = @(
    "bufferizado-cliente",
    "streaming-cliente",
    "streaming-sql-case",
    "dbreader-direto",
    "dbreader-processado"
)

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
dotnet build $projectFile -c Release --nologo | Out-Host

$results = @()

foreach ($quantity in $Quantities) {
    foreach ($scenario in $scenarios) {
        for ($repetition = 1; $repetition -le $Repetitions; $repetition++) {
            Write-Host "Medindo $scenario, $quantity registros, repetição $repetition..."

            $json = & dotnet $dllPath query-benchmark `
                --cenario $scenario `
                --quantidade $quantity `
                --aquecer $WarmUp `
                --forcar-gc $ForceGc

            if ($LASTEXITCODE -ne 0) {
                throw "Falha no cenário $scenario com $quantity registros."
            }

            $measurement = $json | ConvertFrom-Json
            $results += [pscustomobject]@{
                Scenario = $measurement.scenario
                Quantity = $measurement.quantity
                Repetition = $repetition
                BuffersResults = $measurement.buffersResults
                ClientSideEnumConversion = $measurement.clientSideEnumConversion
                DurationMs = $measurement.durationMs
                FileSizeMiB = $measurement.fileSizeMiB
                PeakManagedMemoryDeltaMiB = $measurement.peakManagedMemoryDeltaMiB
                PeakWorkingSetDeltaMiB = $measurement.peakWorkingSetDeltaMiB
                PeakPrivateMemoryDeltaMiB = $measurement.peakPrivateMemoryDeltaMiB
                AllocatedDuringMeasurementMiB = $measurement.allocatedDuringMeasurementMiB
                Generation0Collections = $measurement.generation0Collections
                Generation1Collections = $measurement.generation1Collections
                Generation2Collections = $measurement.generation2Collections
            }
        }
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$csvPath = Join-Path $outputDirectory "query-miniexcel-$timestamp.csv"
$results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
$results | Format-Table -AutoSize
Write-Host "Resultados gravados em $csvPath"
