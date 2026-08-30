[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$config = Get-Content (Join-Path $PSScriptRoot 'pipeline.json') -Raw | ConvertFrom-Json
$unity = $config.unityExe
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity not found: $unity" }
$dotnet = Join-Path (Split-Path $unity -Parent) 'Data\DotNetSdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw "Unity embedded .NET SDK not found: $dotnet" }
$lockFile = Join-Path $root 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockFile) {
    try {
        $lockProbe = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $lockProbe.Dispose()
    }
    catch {
        throw 'This Unity project is currently held by another Editor process. Let that Editor finish or close it normally, then rerun the quality gate.'
    }
}

$editorVersionChannel = $config.unityVersion -replace 'f\d+$', ''
$licensingChannel = "LicenseClient-$([Environment]::UserName)-$editorVersionChannel"
$licensingPipe = "\\.\pipe\Unity-$licensingChannel"
$licensingArguments = if (Test-Path -LiteralPath $licensingPipe) {
    Write-Host "Reusing authorized Unity licensing channel: $licensingChannel"
    @('-licensingIpc', $licensingChannel)
} else { @() }

$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $root "TestResults\Autonomous\$runId"
$uiDir = Join-Path $root 'TestResults\UI'
New-Item -ItemType Directory -Force -Path $runDir, $uiDir | Out-Null
Get-ChildItem -LiteralPath $uiDir -Filter '*.png' -ErrorAction SilentlyContinue | Remove-Item -Force

$steps = [System.Collections.Generic.List[object]]::new()
function Invoke-Unity([string[]]$UnityArguments) {
    $process = Start-Process -FilePath $unity -ArgumentList @($licensingArguments + $UnityArguments) -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -eq 198) {
        throw 'Unity automation exited with 198 because this process did not acquire an Editor entitlement. The Unity Personal seat is already verified; inspect the step log plus Hub/Editor LicensingClient session and project binding instead of requesting another activation.'
    }
    if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode)." }
}
function Invoke-Step([string]$name, [scriptblock]$action) {
    $started = Get-Date
    try {
        & $action
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "$name exited with $LASTEXITCODE" }
        $steps.Add([pscustomobject]@{ name=$name; passed=$true; seconds=[math]::Round(((Get-Date)-$started).TotalSeconds,2); error=$null })
    } catch {
        $steps.Add([pscustomobject]@{ name=$name; passed=$false; seconds=[math]::Round(((Get-Date)-$started).TotalSeconds,2); error=$_.Exception.Message })
        throw
    }
}

$finalPassed = $false
try {
    Push-Location $root
    Invoke-Step 'EditMode' { Invoke-Unity @('-projectPath', $root, '-runTests', '-testPlatform', 'EditMode', '-testResults', (Join-Path $runDir 'EditMode.xml'), '-logFile', (Join-Path $runDir 'EditMode.log')) }
    Invoke-Step 'PlayMode+Screenshots+UIAudit' { Invoke-Unity @('-projectPath', $root, '-runTests', '-testPlatform', 'PlayMode', '-testResults', (Join-Path $runDir 'PlayMode.xml'), '-logFile', (Join-Path $runDir 'PlayMode.log')) }
    Invoke-Step 'BackendDomain' { & (Join-Path $root 'Tools\Verification\run-domain-tests.ps1') *>&1 | Tee-Object -FilePath (Join-Path $runDir 'BackendDomain.log') }
    Invoke-Step 'BackendAuthorityVerification' { & $dotnet run --project (Join-Path $root 'Backend\tests\ImmortalLoot.Server.Verification\ImmortalLoot.Server.Verification.csproj') *>&1 | Tee-Object -FilePath (Join-Path $runDir 'BackendAuthorityVerification.log') }
    Invoke-Step 'BackendHttpContract' { & (Join-Path $root 'Tools\Verification\run-http-contract-tests.ps1') -SkipBuild *>&1 | Tee-Object -FilePath (Join-Path $runDir 'BackendHttpContract.log') }
    Invoke-Step 'UIEvidence' {
        $auditPath = Join-Path $uiDir 'ui-audit.json'
        if (-not (Test-Path $auditPath)) { throw 'UI audit report was not generated.' }
        $audit = Get-Content $auditPath -Raw | ConvertFrom-Json
        $screenshots = @(Get-ChildItem $uiDir -Filter '*.png')
        if (-not $audit.passed) { throw "UI audit failed with $($audit.issueCount) issue(s)." }
        if ($screenshots.Count -lt $config.quality.minimumScreenshots) { throw "Only $($screenshots.Count) UI screenshots were generated." }
    }
    if (-not $SkipBuild) {
        Invoke-Step 'WindowsBuild' { Invoke-Unity @('-quit', '-batchmode', '-projectPath', $root, '-executeMethod', $config.windowsBuildMethod, '-logFile', (Join-Path $runDir 'WindowsBuild.log')) }
        Invoke-Step 'WindowsArtifact' { if (-not (Test-Path (Join-Path $root $config.windowsPlayer))) { throw 'Windows Player artifact is missing.' } }
    }
    Invoke-Step 'GitWhitespace' { & git diff --check; if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed.' } }
    $finalPassed = $true
}
finally {
    Pop-Location
    $report = [ordered]@{ schemaVersion=1; runId=$runId; passed=$finalPassed; generatedAt=(Get-Date).ToString('o'); steps=$steps }
    $json = $report | ConvertTo-Json -Depth 6
    $json | Set-Content -Encoding utf8 (Join-Path $runDir 'result.json')
    $latestDir = Join-Path $root 'TestResults\Autonomous'
    New-Item -ItemType Directory -Force $latestDir | Out-Null
    $json | Set-Content -Encoding utf8 (Join-Path $latestDir 'latest.json')
}

Write-Host "QUALITY GATE PASSED: $runId"
