$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$editorData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data'
$mono = Join-Path $editorData 'MonoBleedingEdge\bin\mono.exe'
$compiler = Join-Path $editorData 'MonoBleedingEdge\lib\mono\4.5\csc.exe'
$output = Join-Path ([IO.Path]::GetTempPath()) ('core-loop-smoke-' + [guid]::NewGuid().ToString('N') + '.exe')
$configRoot = Join-Path $projectRoot 'Assets\Game\Resources\Config'
$pacing = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'demo_pacing.json') | ConvertFrom-Json
$stageFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'stages.json') | ConvertFrom-Json
$loopSource = Join-Path $projectRoot 'Assets\Game\Scripts\Stage\VictoryDrivenStageLoop.cs'

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

Require (Test-Path -LiteralPath $mono) "Unity Mono runtime was not found: $mono"
Require (Test-Path -LiteralPath $compiler) "Unity C# compiler was not found: $compiler"
Require (Test-Path -LiteralPath $loopSource) 'VictoryDrivenStageLoop production seam is missing'

$stageDescriptors = @($stageFile.stages | Sort-Object chapter, stageNumber | ForEach-Object {
    '{0}|{1}|{2}|{3}|{4}|{5}' -f $_.id, $_.chapter, $_.stageNumber, ([bool]$_.isBossStage).ToString(), $_.dropTableId, $_.firstClearDropTableId
})
Require ($stageDescriptors.Count -gt 0) 'stages.json contains no stages'

$sources = @(
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\ConfigContracts.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\GameplayConfigModels.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\DemoPacingConfig.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\DemoPacingSession.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\StageProgressService.cs'),
    $loopSource,
    (Join-Path $PSScriptRoot 'CoreLoopOfflineSmoke.cs')
)

$arguments = @(
    [string]$pacing.durationMinutes,
    [string]$pacing.growthPulseMinutes,
    [string]$pacing.equipmentDropSeconds,
    [string]$pacing.firstEquipmentMinute,
    [string]$pacing.firstBossMinute,
    [string]$pacing.repeatBossMinute,
    [string]$pacing.enemyStatGrowthPercentPerCycle,
    [string]$pacing.rewardGrowthPercentPerCycle,
    [string]$pacing.maxScaledCycle
) + $stageDescriptors

try {
    & $mono $compiler /nologo /target:exe /langversion:9 "/out:$output" $sources
    if ($LASTEXITCODE -ne 0) { throw "Core-loop smoke compilation failed with exit code $LASTEXITCODE" }

    & $mono $output $arguments
    if ($LASTEXITCODE -ne 0) { throw "Core-loop smoke failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item -LiteralPath $output -ErrorAction SilentlyContinue
}
