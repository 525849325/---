#requires -Version 7.0

[CmdletBinding()]
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\DotNetSdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
}

$serverProject = Join-Path $root 'Backend\src\ImmortalLoot.Server\ImmortalLoot.Server.csproj'
$buildOutput = Join-Path $root 'Backend\src\ImmortalLoot.Server\bin\Debug\net8.0'
$runRoot = Join-Path ([IO.Path]::GetTempPath()) ('immortal-loot-http-' + [Guid]::NewGuid().ToString('N'))
$serverRoot = Join-Path $runRoot 'server'
$databasePath = Join-Path $runRoot 'contract.db'
$stdoutPath = Join-Path $runRoot 'server.stdout.log'
$stderrPath = Join-Path $runRoot 'server.stderr.log'
$evidenceRoot = Join-Path $root 'TestResults\HttpContract'
$serverProcess = $null
$passed = $false
$script:assertionCount = 0

function Assert-Contract([bool]$condition, [string]$message) {
    $script:assertionCount++
    if (-not $condition) { throw $message }
}

function Assert-Status($response, [int]$expected, [string]$label) {
    Assert-Contract ([int]$response.StatusCode -eq $expected) `
        "$label returned HTTP $([int]$response.StatusCode), expected $expected. Body: $($response.Content)"
}

function Invoke-ContractRequest {
    param(
        [Parameter(Mandatory)][string]$method,
        [Parameter(Mandatory)][string]$path,
        [object]$body,
        [string]$token
    )

    $arguments = @{
        Method = $method
        Uri = $script:baseUrl + $path
        SkipHttpErrorCheck = $true
        TimeoutSec = 10
    }
    if (-not [string]::IsNullOrWhiteSpace($token)) {
        $arguments.Headers = @{ Authorization = 'Bearer ' + $token }
    }
    if ($PSBoundParameters.ContainsKey('body')) {
        $arguments.ContentType = 'application/json'
        $arguments.Body = $body | ConvertTo-Json -Compress -Depth 8
    }
    Invoke-WebRequest @arguments
}

function Read-Json($response) {
    if ([string]::IsNullOrWhiteSpace($response.Content)) { return $null }
    $response.Content | ConvertFrom-Json
}

try {
    if (-not $SkipBuild) {
        & $dotnet build --configuration Debug $serverProject
        if ($LASTEXITCODE -ne 0) { throw "Backend build failed with exit code $LASTEXITCODE." }
    }

    New-Item -ItemType Directory -Force -Path $serverRoot, $evidenceRoot | Out-Null
    Copy-Item -Path (Join-Path $buildOutput '*') -Destination $serverRoot -Recurse -Force

    $script:baseUrl = $null

    $settings = [ordered]@{
        ConnectionStrings = @{ GameDatabase = 'Data Source=' + $databasePath }
        Payments = @{ EnableMockProvider = $false }
        Urls = 'http://127.0.0.1:0'
        Logging = @{
            LogLevel = @{
                Default = 'Warning'
                'Microsoft.AspNetCore' = 'Warning'
                'Microsoft.Hosting.Lifetime' = 'Information'
            }
            EventLog = @{ LogLevel = @{ Default = 'None' } }
        }
    }
    $settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $serverRoot 'appsettings.HttpContract.json') -Encoding utf8

    $serverDll = Join-Path $serverRoot 'ImmortalLoot.Server.dll'
    $serverProcess = Start-Process `
        -FilePath $dotnet `
        -ArgumentList @($serverDll) `
        -WorkingDirectory $serverRoot `
        -Environment @{ ASPNETCORE_ENVIRONMENT = 'HttpContract'; DOTNET_ENVIRONMENT = 'HttpContract' } `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru

    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $healthy = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($serverProcess.HasExited) {
            throw "Backend exited before the health gate with code $($serverProcess.ExitCode)."
        }
        if ([string]::IsNullOrWhiteSpace($script:baseUrl) -and (Test-Path -LiteralPath $stdoutPath)) {
            $startupLog = [string](Get-Content -LiteralPath $stdoutPath -Raw)
            if (-not [string]::IsNullOrWhiteSpace($startupLog)) {
                $addressMatch = [regex]::Match($startupLog, 'Now listening on:\s+(http://127\.0\.0\.1:\d+)')
                if ($addressMatch.Success) { $script:baseUrl = $addressMatch.Groups[1].Value }
            }
        }
        if (-not [string]::IsNullOrWhiteSpace($script:baseUrl)) {
            try {
                $health = Invoke-WebRequest -Uri ($script:baseUrl + '/health') -SkipHttpErrorCheck -TimeoutSec 2
                if ([int]$health.StatusCode -eq 200) { $healthy = $true; break }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 200
    }
    Assert-Contract $healthy 'Backend did not become healthy within 30 seconds.'

    $unauthorized = Invoke-ContractRequest -Method GET -Path '/player/profile'
    Assert-Status $unauthorized 401 'Unauthenticated profile'

    $loginResponse = Invoke-ContractRequest -Method POST -Path '/auth/login' -Body @{
        provider = 'http-contract'
        externalAccountId = [Guid]::NewGuid().ToString('N')
        nickname = 'HTTPVerifier'
    }
    Assert-Status $loginResponse 200 'Login'
    $login = Read-Json $loginResponse
    Assert-Contract (-not [string]::IsNullOrWhiteSpace([string]$login.accessToken)) 'Login did not return an access token.'
    $token = [string]$login.accessToken

    $profileResponse = Invoke-ContractRequest -Method GET -Path '/player/profile' -Token $token
    Assert-Status $profileResponse 200 'Initial profile'
    $profile = Read-Json $profileResponse
    Assert-Contract ($profile.currentStageId -eq 'stage_1_1') 'Fresh HTTP profile did not expose stage_1_1.'
    Assert-Contract (@($profile.clearedStageIds).Count -eq 0) 'Fresh HTTP profile unexpectedly contained cleared stages.'
    Assert-Contract (($profile.PSObject.Properties.Name -contains 'cultivationExperience') -and [long]$profile.cultivationExperience -eq 0) `
        'Fresh HTTP profile did not expose the independent cumulative cultivation experience field.'
    Assert-Contract (($profile.PSObject.Properties.Name -contains 'breakthroughMaterial') -and [long]$profile.breakthroughMaterial -eq 0) `
        'Fresh HTTP profile did not expose a zero breakthrough-material balance.'
    Assert-Contract (($profile.PSObject.Properties.Name -contains 'pendingTribulation') -and $null -eq $profile.pendingTribulation) `
        'Fresh HTTP profile unexpectedly contained a pending tribulation.'

    $inventory = Invoke-ContractRequest -Method GET -Path '/player/inventory' -Token $token
    Assert-Status $inventory 200 'Initial inventory'

    $insufficientRealm = Invoke-ContractRequest -Method POST -Path '/realm/breakthrough' -Token $token -Body @{
        idempotencyKey = 'http-realm-insufficient'
    }
    Assert-Status $insufficientRealm 409 'Insufficient realm breakthrough'

    $profileAfterRejectedRealmResponse = Invoke-ContractRequest -Method GET -Path '/player/profile' -Token $token
    Assert-Status $profileAfterRejectedRealmResponse 200 'Profile after rejected realm breakthrough'
    $profileAfterRejectedRealm = Read-Json $profileAfterRejectedRealmResponse
    Assert-Contract (
        [int]$profileAfterRejectedRealm.level -eq [int]$profile.level -and
        [long]$profileAfterRejectedRealm.exp -eq [long]$profile.exp -and
        [long]$profileAfterRejectedRealm.cultivationExperience -eq [long]$profile.cultivationExperience -and
        [string]$profileAfterRejectedRealm.realmId -eq [string]$profile.realmId -and
        [int]$profileAfterRejectedRealm.realmStage -eq [int]$profile.realmStage -and
        [string]$profileAfterRejectedRealm.currentStageId -eq [string]$profile.currentStageId) `
        'Rejected realm breakthrough changed player progression.'
    Assert-Contract (
        ($profileAfterRejectedRealm.PSObject.Properties.Name -contains 'breakthroughMaterial') -and
        [long]$profileAfterRejectedRealm.breakthroughMaterial -eq [long]$profile.breakthroughMaterial -and
        ($profileAfterRejectedRealm.PSObject.Properties.Name -contains 'pendingTribulation') -and
        $null -eq $profileAfterRejectedRealm.pendingTribulation -and
        [long]$profileAfterRejectedRealm.softCurrency -eq [long]$profile.softCurrency -and
        [long]$profileAfterRejectedRealm.premiumCurrency -eq [long]$profile.premiumCurrency) `
        'Rejected realm breakthrough changed resources or created a pending tribulation.'

    $noncanonical = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_01'
        idempotencyKey = 'http-noncanonical'
    }
    Assert-Status $noncanonical 400 'Noncanonical battle start'

    $locked = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_2'
        idempotencyKey = 'http-locked'
    }
    Assert-Status $locked 409 'Locked battle start'

    $firstStartResponse = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_1'
        idempotencyKey = 'http-start-original'
    }
    Assert-Status $firstStartResponse 200 'First battle start'
    $firstStart = Read-Json $firstStartResponse

    $recoveredStartResponse = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_1'
        idempotencyKey = 'http-start-after-lost-response'
    }
    Assert-Status $recoveredStartResponse 200 'Lost-response battle recovery'
    $recoveredStart = Read-Json $recoveredStartResponse
    Assert-Contract ($recoveredStart.sessionId -eq $firstStart.sessionId) 'Lost-response recovery created a second battle session.'

    $activeConflict = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_2'
        idempotencyKey = 'http-active-conflict'
    }
    Assert-Status $activeConflict 409 'Different-stage active battle conflict'

    $firstFinishResponse = Invoke-ContractRequest -Method POST -Path '/battle/finish' -Token $token -Body @{
        sessionId = $firstStart.sessionId
        idempotencyKey = 'http-finish-original'
        rewardWindowEligible = $true
    }
    Assert-Status $firstFinishResponse 200 'First battle finish'
    $firstFinish = Read-Json $firstFinishResponse
    Assert-Contract (-not [bool]$firstFinish.replayed) 'First HTTP finish was incorrectly marked as replayed.'
    Assert-Contract ([long]$firstFinish.rewardSoftCurrency -eq 0 -and [long]$firstFinish.rewardExp -eq 0) 'Client reward flag granted normal-stage rewards over HTTP.'

    $finishReplayResponse = Invoke-ContractRequest -Method POST -Path '/battle/finish' -Token $token -Body @{
        sessionId = $firstStart.sessionId
        idempotencyKey = 'http-finish-original'
        rewardWindowEligible = $true
    }
    Assert-Status $finishReplayResponse 200 'Battle finish replay'
    $finishReplay = Read-Json $finishReplayResponse
    Assert-Contract ([bool]$finishReplay.replayed) 'Repeated HTTP finish was not reported as a replay.'

    $advancedProfileResponse = Invoke-ContractRequest -Method GET -Path '/player/profile' -Token $token
    Assert-Status $advancedProfileResponse 200 'Advanced profile'
    $advancedProfile = Read-Json $advancedProfileResponse
    Assert-Contract ($advancedProfile.currentStageId -eq 'stage_1_2') 'HTTP finish did not advance the authoritative profile to stage_1_2.'
    Assert-Contract (@($advancedProfile.clearedStageIds) -contains 'stage_1_1') 'HTTP finish did not persist the cleared stage.'

    $crossStageKey = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_2'
        idempotencyKey = 'http-start-original'
    }
    Assert-Status $crossStageKey 409 'Cross-stage idempotency-key reuse'

    $secondStartResponse = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_2'
        idempotencyKey = 'http-stage-two'
    }
    Assert-Status $secondStartResponse 200 'Authoritative next-stage start'
    $secondStart = Read-Json $secondStartResponse
    Assert-Contract ($secondStart.sessionId -ne $firstStart.sessionId) 'Next-stage HTTP start reused the finished session.'

    $currentStart = $secondStart
    for ($stageNumber = 2; $stageNumber -le 6; $stageNumber++) {
        $finishResponse = Invoke-ContractRequest -Method POST -Path '/battle/finish' -Token $token -Body @{
            sessionId = $currentStart.sessionId
            idempotencyKey = "http-stage-$stageNumber-finish"
            rewardWindowEligible = $false
        }
        Assert-Status $finishResponse 200 "Stage $stageNumber finish"
        if ($stageNumber -lt 6) {
            $nextStageNumber = $stageNumber + 1
            $nextResponse = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
                stageId = "stage_1_$nextStageNumber"
                idempotencyKey = "http-stage-$nextStageNumber-start"
            }
            Assert-Status $nextResponse 200 "Stage $nextStageNumber start"
            $currentStart = Read-Json $nextResponse
        }
    }

    $underpoweredStartResponse = Invoke-ContractRequest -Method POST -Path '/battle/start' -Token $token -Body @{
        stageId = 'stage_1_7'
        idempotencyKey = 'http-underpowered-start'
    }
    Assert-Status $underpoweredStartResponse 200 'Underpowered stage start'
    $underpoweredStart = Read-Json $underpoweredStartResponse
    $underpoweredFinish = Invoke-ContractRequest -Method POST -Path '/battle/finish' -Token $token -Body @{
        sessionId = $underpoweredStart.sessionId
        idempotencyKey = 'http-underpowered-finish'
        rewardWindowEligible = $true
    }
    Assert-Status $underpoweredFinish 409 'Underpowered stage finish'

    $rejectedProfileResponse = Invoke-ContractRequest -Method GET -Path '/player/profile' -Token $token
    Assert-Status $rejectedProfileResponse 200 'Profile after rejected finish'
    $rejectedProfile = Read-Json $rejectedProfileResponse
    Assert-Contract ($rejectedProfile.currentStageId -eq 'stage_1_7') 'Rejected finish advanced the authoritative current stage.'
    Assert-Contract (-not (@($rejectedProfile.clearedStageIds) -contains 'stage_1_7')) 'Rejected finish persisted a cleared stage.'

    $passed = $true
    Write-Host "PASS: real HTTP auth/profile/battle contract ($script:assertionCount assertions)."
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        $serverProcess.Kill($true)
        $serverProcess.WaitForExit()
    }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    foreach ($source in @($stdoutPath, $stderrPath)) {
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $evidenceRoot ($stamp + '-' + (Split-Path $source -Leaf))) -Force
        }
    }
    if (-not $passed) {
        foreach ($source in @($stdoutPath, $stderrPath)) {
            if (Test-Path -LiteralPath $source) {
                Write-Warning ((Split-Path $source -Leaf) + ':')
                Get-Content -LiteralPath $source -Tail 80 | Write-Warning
            }
        }
    }

    $resolvedRunRoot = [IO.Path]::GetFullPath($runRoot)
    $resolvedTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolvedRunRoot.StartsWith($resolvedTempRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove HTTP test directory outside the system temp root: $resolvedRunRoot"
    }
    if (Test-Path -LiteralPath $resolvedRunRoot) {
        Remove-Item -LiteralPath $resolvedRunRoot -Recurse -Force
    }
}
