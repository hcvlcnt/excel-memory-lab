param(
    [int[]]$Quantidades = @(10000, 50000, 100000),
    [int]$Repeticoes = 3,
    [bool]$Aquecer = $true,
    [bool]$ForcarGc = $true
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "OutOfMemoryWorkbook.csproj"
$outputDirectory = Join-Path $projectRoot "Resultados"
$dllPath = Join-Path $projectRoot "bin\Release\net10.0\OutOfMemoryWorkbook.dll"
$cenarios = @(
    "bufferizado-cliente",
    "streaming-cliente",
    "streaming-sql-case",
    "dbreader-direto",
    "dbreader-processado"
)

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
dotnet build $projectFile -c Release --nologo | Out-Host

$resultados = @()

foreach ($quantidade in $Quantidades) {
    foreach ($cenario in $cenarios) {
        for ($repeticao = 1; $repeticao -le $Repeticoes; $repeticao++) {
            Write-Host "Medindo $cenario, $quantidade registros, repeticao $repeticao..."

            $json = & dotnet $dllPath query-benchmark `
                --cenario $cenario `
                --quantidade $quantidade `
                --aquecer $Aquecer `
                --forcar-gc $ForcarGc

            if ($LASTEXITCODE -ne 0) {
                throw "Falha no cenário $cenario com $quantidade registros."
            }

            $medicao = $json | ConvertFrom-Json
            $resultados += [pscustomobject]@{
                Cenario = $medicao.cenario
                Quantidade = $medicao.quantidade
                Repeticao = $repeticao
                BufferizaResultados = $medicao.bufferizaResultados
                ConversaoEnumNoCliente = $medicao.conversaoEnumNoCliente
                DuracaoMs = $medicao.duracaoMs
                TamanhoArquivoMiB = $medicao.tamanhoArquivoMiB
                DeltaPicoMemoriaGerenciadaMiB = $medicao.deltaPicoMemoriaGerenciadaMiB
                DeltaPicoWorkingSetMiB = $medicao.deltaPicoWorkingSetMiB
                DeltaPicoMemoriaPrivadaMiB = $medicao.deltaPicoMemoriaPrivadaMiB
                AlocadoDuranteMedicaoMiB = $medicao.alocadoDuranteMedicaoMiB
                ColetasGeracao0 = $medicao.coletasGeracao0
                ColetasGeracao1 = $medicao.coletasGeracao1
                ColetasGeracao2 = $medicao.coletasGeracao2
            }
        }
    }
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$csvPath = Join-Path $outputDirectory "query-miniexcel-$timestamp.csv"
$resultados | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8
$resultados | Format-Table -AutoSize
Write-Host "Resultados gravados em $csvPath"
