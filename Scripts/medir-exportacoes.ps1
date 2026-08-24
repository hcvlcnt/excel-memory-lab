param(
    [int[]] $Quantidades = @(10000, 50000, 100000),
    [ValidateRange(1, 20)]
    [int] $Repeticoes = 3,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuracao = 'Release',
    [switch] $SemBuild
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'OutOfMemoryWorkbook.csproj'
$applicationDll = Join-Path $projectRoot "bin\$Configuracao\net10.0\OutOfMemoryWorkbook.dll"
$resultsDirectory = Join-Path $projectRoot 'Resultados'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputFile = Join-Path $resultsDirectory "medicoes-$timestamp.csv"

if (-not $SemBuild)
{
    dotnet build $projectFile --configuration $Configuracao

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

$results = foreach ($quantity in $Quantidades)
{
    foreach ($scenario in $scenarios)
    {
        foreach ($repetition in 1..$Repeticoes)
        {
            Write-Host "Medindo $scenario com $quantity registros (repetição $repetition/$Repeticoes)..."

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
                Cenario = $measurement.cenario
                Quantidade = $measurement.quantidade
                Repeticao = $repetition
                DuracaoMs = $measurement.duracaoMs
                TamanhoArquivoMiB = $measurement.tamanhoArquivoMiB
                DeltaPicoMemoriaGerenciadaMiB = $measurement.deltaPicoMemoriaGerenciadaMiB
                DeltaPicoWorkingSetMiB = $measurement.deltaPicoWorkingSetMiB
                DeltaPicoMemoriaPrivadaMiB = $measurement.deltaPicoMemoriaPrivadaMiB
                AlocadoDuranteMedicaoMiB = $measurement.alocadoDuranteMedicaoMiB
                ColetasGeracao0 = $measurement.coletasGeracao0
                ColetasGeracao1 = $measurement.coletasGeracao1
                ColetasGeracao2 = $measurement.coletasGeracao2
                QuantidadeAmostras = $measurement.quantidadeAmostras
                IntervaloAmostragemMs = $measurement.intervaloAmostragemMs
                AquecimentoExecutado = $measurement.aquecimentoExecutado
                GcForcado = $measurement.gcForcado
                DestinoMedicao = $measurement.destinoMedicao
            }
        }
    }
}

New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$results | Export-Csv -LiteralPath $outputFile -Delimiter ';' -NoTypeInformation -Encoding utf8
$results | Format-Table Cenario, Quantidade, Repeticao, DuracaoMs, DeltaPicoMemoriaGerenciadaMiB, DeltaPicoWorkingSetMiB -AutoSize

Write-Host "Resultados salvos em: $outputFile"
