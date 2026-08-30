$ErrorActionPreference = 'Stop'
$editorData = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data'
$mono = Join-Path $editorData 'MonoBleedingEdge\bin\mono.exe'
$compiler = Join-Path $editorData 'MonoBleedingEdge\lib\mono\4.5\csc.exe'
$output = Join-Path $PSScriptRoot 'DomainSmokeTests.exe'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sources = @(
    (Join-Path $projectRoot 'Assets\Game\Scripts\Core\IRandomSource.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Core\SystemRandomSource.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\ConfigContracts.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\GameConfigCatalog.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Config\GameplayConfigModels.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Character\CharacterStats.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Character\StatModifier.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Equipment\EquipmentModels.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Equipment\AffixGenerator.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Equipment\EquipmentGenerator.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Inventory\InventoryModels.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Inventory\InventoryOverflowPolicy.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Battle\DamageCalculator.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Battle\AutoBattleEngine.cs'),
    (Join-Path $projectRoot 'Assets\Game\Scripts\Analytics\ValidationFunnelTelemetry.cs'),
    (Join-Path $PSScriptRoot 'DomainSmokeTests.cs')
)

try {
    & $mono $compiler /nologo /target:exe /langversion:9 "/out:$output" $sources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE" }
    & $mono $output
    if ($LASTEXITCODE -ne 0) { throw "Verification failed with exit code $LASTEXITCODE" }
}
finally {
    Remove-Item -LiteralPath $output -ErrorAction SilentlyContinue
}
