param(
    [int[]] $Quantities = @(10000, 50000, 100000),
    [ValidateRange(1, 20)]
    [int] $Repetitions = 3,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'OutOfMemoryWorkbook.csproj'
$applicationDll = Join-Path $projectRoot "bin\$Configuration\net10.0\OutOfMemoryWorkbook.dll"
$resultsDirectory = Join-Path $projectRoot 'Resultados'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputFile = Join-Path $resultsDirectory "medicoes-$timestamp.csv"

if (-not $SkipBuild)
{
    dotnet build $projectFile --configuration $Configuration

    if ($LASTEXITCODE -ne 0)
    {
        throw 'A compilação falhou.'
    }
}

if (-not (Test-Path -LiteralPath $applicationDll))
{
    throw "Executável não encontrado: $applicationDll"
}

$scenarios = @(
    'atual',
    'xssf-sem-to-array',
    'sxssf-com-lista',
    'sxssf-stream-arquivo',
    'sxssf-stream-response'
)

$results = foreach ($quantity in $Quantities)
{
    foreach ($scenario in $scenarios)
    {
        foreach ($repetition in 1..$Repetitions)
        {
            Write-Host "Medindo $scenario com $quantity registros (repetição $repetition/$Repetitions)..."

            $json = dotnet $applicationDll benchmark `
                --cenario $scenario `
                --quantidade $quantity `
                --aquecer true `
                --forcar-gc true

            if ($LASTEXITCODE -ne 0)
            {
                throw "A medição do cenário '$scenario' falhou."
            }

            $measurement = $json | ConvertFrom-Json

            [PSCustomObject]@{
                Scenario = $measurement.scenario
                Quantity = $measurement.quantity
                Repetition = $repetition
                DurationMs = $measurement.durationMs
                FileSizeMiB = $measurement.fileSizeMiB
                PeakManagedMemoryDeltaMiB = $measurement.peakManagedMemoryDeltaMiB
                PeakWorkingSetDeltaMiB = $measurement.peakWorkingSetDeltaMiB
                PeakPrivateMemoryDeltaMiB = $measurement.peakPrivateMemoryDeltaMiB
                AllocatedDuringMeasurementMiB = $measurement.allocatedDuringMeasurementMiB
                Generation0Collections = $measurement.generation0Collections
                Generation1Collections = $measurement.generation1Collections
                Generation2Collections = $measurement.generation2Collections
                SampleCount = $measurement.sampleCount
                SamplingIntervalMs = $measurement.samplingIntervalMs
                WarmUpExecuted = $measurement.warmUpExecuted
                GcForced = $measurement.gcForced
                MeasurementTarget = $measurement.measurementTarget
            }
        }
    }
}

New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$results | Export-Csv -LiteralPath $outputFile -Delimiter ';' -NoTypeInformation -Encoding utf8
$results | Format-Table Scenario, Quantity, Repetition, DurationMs, PeakManagedMemoryDeltaMiB, PeakWorkingSetDeltaMiB -AutoSize

Write-Host "Resultados salvos em: $outputFile"
