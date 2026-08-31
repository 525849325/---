$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$editorData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data'
$mono = Join-Path $editorData 'MonoBleedingEdge\bin\mono.exe'
$compiler = Join-Path $editorData 'MonoBleedingEdge\lib\mono\4.5\csc.exe'
$output = Join-Path ([IO.Path]::GetTempPath()) ('real-battle-pacing-' + [guid]::NewGuid().ToString('N') + '.exe')
$configRoot = Join-Path $projectRoot 'Assets\Game\Resources\Config'

$pacing = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'demo_pacing.json') | ConvertFrom-Json
$formula = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'battle_formula.json') | ConvertFrom-Json
$skillFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'skills.json') | ConvertFrom-Json
$monsterFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'monsters.json') | ConvertFrom-Json
$stageFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'stages.json') | ConvertFrom-Json
$realmFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'realms.json') | ConvertFrom-Json
$realmFormula = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'realm_formula.json') | ConvertFrom-Json

$firstRealm = @($realmFile.realms | Sort-Object order)[0]
$firstBoss = @($stageFile.stages | Where-Object { $_.isBossStage } | Sort-Object chapter, stageNumber)[0]
$firstMinorCost = [math]::Max(1, [math]::Ceiling(
    [double]$firstRealm.breakthroughCost * [double]$realmFormula.minorCostScale / [double]$firstRealm.stageCount))
if ($null -eq $firstBoss -or [long]$firstBoss.rewardBreakthroughMaterial -lt [long]$firstMinorCost) {
    throw "The first Boss must fund at least one configured minor realm breakthrough (needs $firstMinorCost)."
}

function Invariant([object]$value) {
    return [Convert]::ToString($value, [Globalization.CultureInfo]::InvariantCulture)
}

$skillDescriptors = @($skillFile.skills | ForEach-Object {
    'skill|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}' -f
        $_.id, $_.element, $_.type, (Invariant $_.cooldown), (Invariant $_.multiplier),
        $_.targetType, $_.effectType, (Invariant $_.effectValue), (Invariant $_.duration)
})
$monsterDescriptors = @($monsterFile.monsters | ForEach-Object {
    'monster|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}' -f
        $_.id, $_.rank, (Invariant $_.maxHp), (Invariant $_.attack), (Invariant $_.defense),
        (Invariant $_.attackInterval), (Invariant $_.enrageSeconds), $_.dropTableId, (@($_.skillIds) -join ',')
})
$stageDescriptors = @($stageFile.stages | Sort-Object chapter, stageNumber | ForEach-Object {
    'stage|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}' -f
        $_.id, $_.chapter, $_.stageNumber, ([bool]$_.isBossStage).ToString(),
        $_.rewardExp, $_.rewardSoftCurrency, $_.dropTableId, $_.firstClearDropTableId,
        (Invariant $_.afkRewardRate), (@($_.monsterGroup) -join ',')
})

$arguments = @(
    [string]$pacing.durationMinutes,
    [string]$pacing.growthPulseMinutes,
    [string]$pacing.equipmentDropSeconds,
    [string]$pacing.firstEquipmentMinute,
    [string]$pacing.firstBossMinute,
    [string]$pacing.repeatBossMinute,
    [string]$pacing.enemyStatGrowthPercentPerCycle,
    [string]$pacing.rewardGrowthPercentPerCycle,
    [string]$pacing.maxScaledCycle,
    (Invariant $formula.defenseConstant),
    (Invariant $formula.minimumDamage),
    (Invariant $formula.maximumDamageReduction),
    (Invariant $formula.maximumElementResistance)
) + $skillDescriptors + $monsterDescriptors + $stageDescriptors

$sources = @(
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\ConfigContracts.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\GameplayConfigModels.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\GameConfigCatalog.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Character\CharacterStats.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Character\StatModifier.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Equipment\EquipmentModels.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Core\IRandomSource.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Core\SystemRandomSource.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Battle\DamageCalculator.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Battle\AutoBattleEngine.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\MonsterFactory.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\StageBattleFactory.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\CycleScalingPolicy.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\DemoPacingConfig.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\DemoPacingSession.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\StageProgressService.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Stage\VictoryDrivenStageLoop.cs'),
    (Join-Path $PSScriptRoot 'RealBattlePacingSmoke.cs')
)

try {
    & $mono $compiler /nologo /target:exe /langversion:9 "/out:$output" $sources
    if ($LASTEXITCODE -ne 0) { throw "Real-battle pacing compilation failed with exit code $LASTEXITCODE" }

    & $mono $output $arguments
    if ($LASTEXITCODE -ne 0) { throw "Real-battle pacing smoke failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item -LiteralPath $output -ErrorAction SilentlyContinue
}
