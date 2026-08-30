$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$configRoot = Join-Path $projectRoot 'Assets\Game\Resources\Config'
$pacing = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'demo_pacing.json') | ConvertFrom-Json
$dropFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'drop_tables.json') | ConvertFrom-Json
$equipmentFile = Get-Content -Raw -Encoding UTF8 (Join-Path $configRoot 'equipment.json') | ConvertFrom-Json

function Require([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function RewardCount([int]$minutes) {
    $window = $minutes * 60
    $first = $pacing.firstEquipmentMinute * 60
    if ($window -lt $first) { return 0 }
    return 1 + [math]::Floor(($window - $first) / $pacing.equipmentDropSeconds)
}

$prototype = $dropFile.dropTables | Where-Object id -eq 'drop_prototype_equipment'
$boss = $dropFile.dropTables | Where-Object id -eq 'drop_boss_1'
$knownEquipment = @{}
foreach ($item in $equipmentFile.equipment) { $knownEquipment[$item.id] = $true }
Require ($null -ne $prototype) 'prototype equipment table is missing'
Require ($null -ne $boss) 'boss table is missing'
Require ($prototype.entries.Count -eq 10) 'prototype table must expose all ten equipment slots'
foreach ($entry in @($prototype.entries) + @($boss.entries)) {
    Require ($knownEquipment.ContainsKey($entry.itemId)) "drop entry references unknown equipment: $($entry.itemId)"
    Require ($entry.weight -gt 0) "drop entry has non-positive weight: $($entry.itemId)"
}
foreach ($entry in $boss.entries) { Require ($entry.minQuality -eq 'Rare') 'boss reward must be Rare or better' }

$random = [System.Random]::new(20260830)
$slotCounts = @{}
$qualityCounts = @{ Fine = 0; Rare = 0; Epic = 0; Legendary = 0 }
$totalWeight = ($prototype.entries | Measure-Object weight -Sum).Sum
$qualityWeights = @(4, 3, 2, 1)
$qualityNames = @('Fine', 'Rare', 'Epic', 'Legendary')
for ($rollIndex = 0; $rollIndex -lt 10000; $rollIndex++) {
    $entryRoll = $random.Next(0, $totalWeight)
    $selected = $null
    foreach ($entry in $prototype.entries) {
        $entryRoll -= $entry.weight
        if ($entryRoll -lt 0) { $selected = $entry; break }
    }
    if (-not $slotCounts.ContainsKey($selected.itemId)) { $slotCounts[$selected.itemId] = 0 }
    $slotCounts[$selected.itemId]++

    $qualityRoll = $random.Next(0, 10)
    for ($qualityIndex = 0; $qualityIndex -lt $qualityWeights.Count; $qualityIndex++) {
        $qualityRoll -= $qualityWeights[$qualityIndex]
        if ($qualityRoll -lt 0) { $qualityCounts[$qualityNames[$qualityIndex]]++; break }
    }
}

Require ($slotCounts.Count -eq 10) 'large sample did not reach every slot'
foreach ($count in $slotCounts.Values) { Require ($count -ge 800 -and $count -le 1200) "slot distribution outside 8%-12% band: $count" }
Require ($qualityCounts.Fine -ge 3700 -and $qualityCounts.Fine -le 4300) 'Fine distribution outside 37%-43% band'
Require ($qualityCounts.Rare -ge 2700 -and $qualityCounts.Rare -le 3300) 'Rare distribution outside 27%-33% band'
Require ($qualityCounts.Epic -ge 1700 -and $qualityCounts.Epic -le 2300) 'Epic distribution outside 17%-23% band'
Require ($qualityCounts.Legendary -ge 700 -and $qualityCounts.Legendary -le 1300) 'Legendary distribution outside 7%-13% band'

$tenMinuteDrops = RewardCount 10
$sixtyMinuteDrops = RewardCount 60
Require ($tenMinuteDrops -ge 20 -and $tenMinuteDrops -le 25) "10-minute drop count outside target: $tenMinuteDrops"
Require ($sixtyMinuteDrops -ge 130 -and $sixtyMinuteDrops -le 150) "60-minute drop count outside target: $sixtyMinuteDrops"
Require ($pacing.firstBossMinute -ge 2 -and $pacing.firstBossMinute -le 4) 'first Boss is outside 2-4 minute target'
Require ($pacing.firstRealmBreakthroughMinute -ge 3 -and $pacing.firstRealmBreakthroughMinute -le 6) 'first realm breakthrough is outside 3-6 minute target'

Write-Output "PASS: deterministic balance simulation"
Write-Output "10m drops=$tenMinuteDrops; 60m drops=$sixtyMinuteDrops; boss=$($pacing.firstBossMinute)m; realm=$($pacing.firstRealmBreakthroughMinute)m"
Write-Output "Quality Fine=$($qualityCounts.Fine) Rare=$($qualityCounts.Rare) Epic=$($qualityCounts.Epic) Legendary=$($qualityCounts.Legendary)"
Write-Output "Slots $((($slotCounts.GetEnumerator() | Sort-Object Name | ForEach-Object { $_.Name + '=' + $_.Value }) -join ', '))"
