#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$AdbPath = "",
    [string]$Serial = "",
    [string]$ApkPath = "",
    [ValidateRange(1, 720)]
    [int]$DurationMinutes = 120,
    [ValidateRange(5, 60)]
    [int]$SampleIntervalSeconds = 60,
    [switch]$SkipInstall,
    [switch]$FreshInstall,
    [switch]$DeviceCheckOnly
)

$ErrorActionPreference = "Stop"
$packageName = "com.immortalloot.prototype"
$activityName = "com.unity3d.player.UnityPlayerActivity"
$expectedVersionName = "0.1.0"
$expectedVersionCode = "1"
$workspaceRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

function Resolve-Adb {
    if ($AdbPath) {
        return (Resolve-Path -LiteralPath $AdbPath).Path
    }

    $pathCommand = Get-Command adb -ErrorAction SilentlyContinue
    if ($pathCommand) { return $pathCommand.Source }

    $unityAdb = "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    if (Test-Path -LiteralPath $unityAdb) { return $unityAdb }
    throw "ADB was not found. Pass -AdbPath or install Unity Android SDK tools."
}

function Invoke-Adb {
    param([Parameter(Mandatory)][string[]]$Arguments, [switch]$AllowFailure)
    $output = & $script:adbExecutable @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).TrimEnd()
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "adb $($Arguments -join ' ') failed with exit code ${exitCode}:`n$text"
    }
    return $text
}

function Get-Property {
    param([Parameter(Mandatory)][string]$Name)
    return (Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "getprop", $Name)).Trim()
}

function Get-SafeFileName {
    param([Parameter(Mandatory)][string]$Value)
    return ($Value -replace '[^A-Za-z0-9._-]', '_')
}

$script:adbExecutable = Resolve-Adb
$deviceLines = Invoke-Adb -Arguments @("devices", "-l")
$connected = @($deviceLines -split "`r?`n" | Where-Object { $_ -match '^\S+\s+device(?:\s|$)' })
if ($Serial) {
    $matched = @($connected | Where-Object { ($_ -split '\s+')[0] -eq $Serial })
    if ($matched.Count -ne 1) { throw "Requested device '$Serial' is not connected and authorized." }
    $script:deviceSerial = $Serial
} else {
    if ($connected.Count -ne 1) {
        throw "Exactly one authorized Android device is required; found $($connected.Count). Pass -Serial when multiple devices are connected.`n$deviceLines"
    }
    $script:deviceSerial = ($connected[0] -split '\s+')[0]
}

$properties = [ordered]@{
    Serial       = $script:deviceSerial
    Manufacturer = Get-Property "ro.product.manufacturer"
    Brand        = Get-Property "ro.product.brand"
    Model        = Get-Property "ro.product.model"
    Device       = Get-Property "ro.product.device"
    Hardware     = Get-Property "ro.hardware"
    Product      = Get-Property "ro.build.product"
    Android      = Get-Property "ro.build.version.release"
    ApiLevel     = Get-Property "ro.build.version.sdk"
    AbiList      = Get-Property "ro.product.cpu.abilist"
    KernelQemu   = Get-Property "ro.kernel.qemu"
}

$emulatorFingerprint = ($properties.Values -join " ").ToLowerInvariant()
$emulatorMarkers = @("nox", "vbox", "virtualbox", "goldfish", "ranchu", "generic_x86", "android sdk built for", "genymotion")
$matchedMarkers = @($emulatorMarkers | Where-Object { $emulatorFingerprint.Contains($_) })
$networkSerial = $script:deviceSerial -match '^(127\.0\.0\.1|localhost|emulator-)'
if ($properties.KernelQemu -eq "1" -or $networkSerial -or $matchedMarkers.Count -gt 0) {
    $reason = @()
    if ($properties.KernelQemu -eq "1") { $reason += "ro.kernel.qemu=1" }
    if ($networkSerial) { $reason += "emulator/network serial '$($script:deviceSerial)'" }
    if ($matchedMarkers.Count -gt 0) { $reason += "markers: $($matchedMarkers -join ', ')" }
    throw "Physical-device gate rejected this target ($($reason -join '; ')). Device: $($properties.Manufacturer) $($properties.Model), hardware=$($properties.Hardware)."
}

$apiLevel = 0
if (-not [int]::TryParse($properties.ApiLevel, [ref]$apiLevel) -or $apiLevel -lt 26) {
    throw "Android API 26 or newer is required; device reported API '$($properties.ApiLevel)'."
}

Write-Host "Physical Android device accepted: $($properties.Manufacturer) $($properties.Model), Android $($properties.Android) / API $($properties.ApiLevel), serial $($properties.Serial)"
if ($DeviceCheckOnly) { return }
if ($SkipInstall) { throw "-SkipInstall is not permitted for an RC acceptance run because it cannot prove the installed binary matches the selected APK." }

if (-not $ApkPath) { $ApkPath = Join-Path $workspaceRoot "Build\Android\Taichu-Endless-Reincarnation-0.1.0-rc.apk" }
$resolvedApk = (Resolve-Path -LiteralPath $ApkPath).Path
$apkFile = Get-Item -LiteralPath $resolvedApk
$apkHash = (Get-FileHash -LiteralPath $resolvedApk -Algorithm SHA256).Hash
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$safeSerial = Get-SafeFileName $script:deviceSerial
$evidenceDirectory = Join-Path $workspaceRoot "Docs\Evidence\AndroidPhysical\$timestamp-$safeSerial"
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$properties.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" } | Set-Content -LiteralPath (Join-Path $evidenceDirectory "device-properties.txt") -Encoding utf8
@(
    "Path=$resolvedApk"
    "Bytes=$($apkFile.Length)"
    "SHA256=$apkHash"
) | Set-Content -LiteralPath (Join-Path $evidenceDirectory "artifact-metadata.txt") -Encoding utf8

$installMode = "replace-preserve-data"
if ($FreshInstall) {
    $preInstallPackagePath = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "pm", "path", $packageName) -AllowFailure
    $preInstallPackagePath | Set-Content -LiteralPath (Join-Path $evidenceDirectory "preinstall-package-path.txt") -Encoding utf8
    if ($preInstallPackagePath -match '(?m)^package:') {
        Invoke-Adb -Arguments @("-s", $script:deviceSerial, "uninstall", $packageName) |
            Set-Content -LiteralPath (Join-Path $evidenceDirectory "uninstall.txt") -Encoding utf8
        $afterUninstallPackagePath = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "pm", "path", $packageName) -AllowFailure
        $afterUninstallPackagePath | Set-Content -LiteralPath (Join-Path $evidenceDirectory "postuninstall-package-path.txt") -Encoding utf8
        if ($afterUninstallPackagePath -match '(?m)^package:') {
            throw "Fresh-install gate failed: package '$packageName' still exists after adb uninstall."
        }
        if (-not [string]::IsNullOrWhiteSpace($afterUninstallPackagePath)) {
            throw "Fresh-install gate could not verify package removal: $afterUninstallPackagePath"
        }
        $installMode = "fresh-after-verified-uninstall"
    }
    elseif ([string]::IsNullOrWhiteSpace($preInstallPackagePath)) {
        "Package was not installed; no uninstall was required." |
            Set-Content -LiteralPath (Join-Path $evidenceDirectory "uninstall.txt") -Encoding utf8
        $installMode = "fresh-package-not-previously-installed"
    }
    else {
        throw "Fresh-install gate could not determine whether '$packageName' is installed: $preInstallPackagePath"
    }
}
Invoke-Adb -Arguments @("-s", $script:deviceSerial, "install", "-r", $resolvedApk) |
    Set-Content -LiteralPath (Join-Path $evidenceDirectory "install.txt") -Encoding utf8

$packageInfo = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "package", $packageName) -AllowFailure
$packageInfo | Set-Content -LiteralPath (Join-Path $evidenceDirectory "package-info.txt") -Encoding utf8
$versionNameMatch = [regex]::Match($packageInfo, '(?m)^\s*versionName=(\S+)')
$versionCodeMatch = [regex]::Match($packageInfo, '(?m)^\s*versionCode=(\d+)')
$installedVersionName = if ($versionNameMatch.Success) { $versionNameMatch.Groups[1].Value } else { "unknown" }
$installedVersionCode = if ($versionCodeMatch.Success) { $versionCodeMatch.Groups[1].Value } else { "unknown" }
if ($installedVersionName -ne $expectedVersionName -or $installedVersionCode -ne $expectedVersionCode) {
    throw "Installed package version '$installedVersionName ($installedVersionCode)' does not match RC '$expectedVersionName ($expectedVersionCode)'. Do not continue against the wrong build."
}

$script:observedPids = [System.Collections.Generic.HashSet[string]]::new()
$script:fatalLines = [System.Collections.Generic.HashSet[string]]::new()
$script:processMissingSamples = 0
$script:processRestartCount = 0
$processObservationFile = Join-Path $evidenceDirectory "process-observations.csv"
$logcatStreamFile = Join-Path $evidenceDirectory "logcat-stream.txt"
$logcatTailFile = Join-Path $evidenceDirectory "logcat-tail.txt"
$logcatErrorFile = Join-Path $evidenceDirectory "logcat-stderr.txt"
"UtcTime,Reason,Running,Pids" | Set-Content -LiteralPath $processObservationFile -Encoding utf8

function Get-UtcTimestamp {
    return (Get-Date).ToUniversalTime().ToString("o")
}

function Get-AppPids {
    $pidText = (Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "pidof", $packageName) -AllowFailure).Trim()
    if ([string]::IsNullOrWhiteSpace($pidText)) { return @() }
    return @($pidText -split '\s+' | Where-Object { $_ -match '^\d+$' })
}

function Observe-AppProcess {
    param([Parameter(Mandatory)][string]$Reason)
    $currentPids = @(Get-AppPids)
    if ($currentPids.Count -eq 0) {
        $script:processMissingSamples++
        "$(Get-UtcTimestamp),$Reason,false," | Add-Content -LiteralPath $processObservationFile -Encoding utf8
        return @()
    }

    foreach ($currentPid in $currentPids) {
        if ($script:observedPids.Add($currentPid) -and $script:observedPids.Count -gt 1) {
            $script:processRestartCount++
        }
    }
    "$(Get-UtcTimestamp),$Reason,true,$($currentPids -join ' ')" | Add-Content -LiteralPath $processObservationFile -Encoding utf8
    return $currentPids
}

function Assert-LogcatCapture {
    if ($script:logcatProcess.HasExited) {
        throw "The continuous logcat collector exited unexpectedly with code $($script:logcatProcess.ExitCode). See logcat-stderr.txt."
    }
}

Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "am", "force-stop", $packageName) | Out-Null
# Start with a known-empty buffer; failure is a gate failure, not a warning.
Invoke-Adb -Arguments @("-s", $script:deviceSerial, "logcat", "-c") | Out-Null
$script:logcatProcess = Start-Process -FilePath $script:adbExecutable -ArgumentList @("-s", $script:deviceSerial, "logcat", "-v", "threadtime") -RedirectStandardOutput $logcatStreamFile -RedirectStandardError $logcatErrorFile -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Milliseconds 200
    Assert-LogcatCapture
    Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "am", "start", "-W", "-n", "$packageName/$activityName") | Set-Content -LiteralPath (Join-Path $evidenceDirectory "launch.txt") -Encoding utf8

    $initialPids = @()
    for ($attempt = 0; $attempt -lt 20 -and $initialPids.Count -eq 0; $attempt++) {
        $initialPids = @(Get-AppPids)
        if ($initialPids.Count -eq 0) { Start-Sleep -Milliseconds 250 }
    }
    if ($initialPids.Count -eq 0) {
        throw "The app process did not start within five seconds; inspect launch.txt and logcat-stream.txt."
    }
    foreach ($initialPid in $initialPids) { $null = $script:observedPids.Add($initialPid) }
    $appPid = $initialPids[0]
    "$(Get-UtcTimestamp),launch,true,$($initialPids -join ' ')" | Add-Content -LiteralPath $processObservationFile -Encoding utf8
    @("InitialPids=$($initialPids -join ' ')", "ExpectedVersion=$expectedVersionName ($expectedVersionCode)") | Set-Content -LiteralPath (Join-Path $evidenceDirectory "runtime-process.txt") -Encoding utf8

    $resumedActivityPattern = '(?m)^\s*(?:mResumedActivity|topResumedActivity|ResumedActivity).*' + [regex]::Escape("$packageName/")
    $initialForeground = $false
    for ($attempt = 0; $attempt -lt 20 -and -not $initialForeground; $attempt++) {
        $startupActivity = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "activity", "activities") -AllowFailure
        $initialForeground = $startupActivity -match $resumedActivityPattern
        if (-not $initialForeground) { Start-Sleep -Milliseconds 250 }
    }
    if (-not $initialForeground) {
        throw "The app did not reach the resumed foreground state within five seconds."
    }
    Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "gfxinfo", $packageName, "reset") -AllowFailure | Out-Null

    $sampleFile = Join-Path $evidenceDirectory "samples.csv"
    "UtcTime,ElapsedSeconds,Foreground,PssTotalKb,BatteryLevel,BatteryTemperatureTenthsC" | Set-Content -LiteralPath $sampleFile -Encoding utf8
    $started = Get-Date
    $deadline = $started.AddMinutes($DurationMinutes)
    $sampleIndex = 0
    do {
        Assert-LogcatCapture
        $elapsed = [int]((Get-Date) - $started).TotalSeconds
        $currentPids = @(Observe-AppProcess -Reason ("sample-{0:D4}" -f $sampleIndex))
        $activity = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "activity", "activities") -AllowFailure
        $foreground = if ($activity -match $resumedActivityPattern) { "true" } else { "false" }
        $meminfo = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "meminfo", $packageName) -AllowFailure
        $pssMatch = [regex]::Match($meminfo, '(?m)^\s*TOTAL\s+(\d+)')
        $pssKb = if ($pssMatch.Success) { $pssMatch.Groups[1].Value } else { "" }
        $battery = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "battery") -AllowFailure
        $levelMatch = [regex]::Match($battery, '(?m)^\s*level:\s*(\d+)')
        $temperatureMatch = [regex]::Match($battery, '(?m)^\s*temperature:\s*(\d+)')
        $level = if ($levelMatch.Success) { $levelMatch.Groups[1].Value } else { "" }
        $temperature = if ($temperatureMatch.Success) { $temperatureMatch.Groups[1].Value } else { "" }
        "$(Get-UtcTimestamp),$elapsed,$foreground,$pssKb,$level,$temperature" | Add-Content -LiteralPath $sampleFile -Encoding utf8
        $meminfo | Set-Content -LiteralPath (Join-Path $evidenceDirectory ("meminfo-{0:D4}.txt" -f $sampleIndex)) -Encoding utf8
        Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "thermalservice") -AllowFailure | Set-Content -LiteralPath (Join-Path $evidenceDirectory ("thermal-{0:D4}.txt" -f $sampleIndex)) -Encoding utf8
        $sampleIndex++
        if ((Get-Date) -lt $deadline) { Start-Sleep -Seconds $SampleIntervalSeconds }
    } while ((Get-Date) -lt $deadline)

    Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "gfxinfo", $packageName, "framestats") -AllowFailure | Set-Content -LiteralPath (Join-Path $evidenceDirectory "gfxinfo-framestats.txt") -Encoding utf8
    $null = @(Observe-AppProcess -Reason "final")
    Assert-LogcatCapture
}
finally {
    if ($script:logcatProcess -and -not $script:logcatProcess.HasExited) {
        Stop-Process -Id $script:logcatProcess.Id -ErrorAction SilentlyContinue
        Wait-Process -Id $script:logcatProcess.Id -Timeout 5 -ErrorAction SilentlyContinue
        $script:logcatProcess.Refresh()
        if (-not $script:logcatProcess.HasExited) {
            throw "The continuous logcat collector could not be stopped cleanly."
        }
    }
    $logcatTail = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "logcat", "-d", "-v", "threadtime") -AllowFailure
    $logcatTail | Set-Content -LiteralPath $logcatTailFile -Encoding utf8
}

function Test-FatalLogcatLine {
    param([AllowEmptyString()][string]$Line)
    if ($Line -match "ANR in $([regex]::Escape($packageName))") {
        $null = $script:fatalLines.Add($Line)
        return
    }
    if ($Line -notmatch 'FATAL EXCEPTION|Fatal signal') { return }
    foreach ($observedPid in $script:observedPids) {
        if ($Line -match "^\S+\s+\S+\s+$([regex]::Escape($observedPid))\s+") {
            $null = $script:fatalLines.Add($Line)
            return
        }
    }
}

foreach ($logPath in @($logcatStreamFile, $logcatTailFile)) {
    if (-not (Test-Path -LiteralPath $logPath)) { continue }
    Get-Content -LiteralPath $logPath | ForEach-Object { Test-FatalLogcatLine -Line $_ }
}
$script:fatalLines | Set-Content -LiteralPath (Join-Path $evidenceDirectory "fatal-anr.txt") -Encoding utf8
@(
    "ObservedPids=$(@($script:observedPids) -join ' ')"
    "MissingSamples=$($script:processMissingSamples)"
    "RestartCount=$($script:processRestartCount)"
) | Add-Content -LiteralPath (Join-Path $evidenceDirectory "runtime-process.txt") -Encoding utf8

$template = @(
    '# ImmortalLoot 低端 Android 物理真机验收'
    ''
    "- 设备：$($properties.Manufacturer) $($properties.Model)"
    "- Android / API：$($properties.Android) / $($properties.ApiLevel)"
    "- ABI：$($properties.AbiList)"
    "- 序列号：$($properties.Serial)"
    "- APK：$resolvedApk"
    "- APK 字节数：$($apkFile.Length)"
    "- APK SHA-256：$apkHash"
    "- 已安装版本：$installedVersionName ($installedVersionCode)"
    "- 安装模式：$installMode"
    "- 启动 PID：$appPid"
    "- 采集开始：$($started.ToString('o'))"
    "- 计划时长：$DurationMinutes 分钟"
    "- 自动采样数：$sampleIndex"
    "- 观察到的 PID：$(@($script:observedPids) -join ', ')"
    "- 进程缺失采样：$($script:processMissingSamples)"
    "- 进程重启计数：$($script:processRestartCount)"
    "- FATAL/ANR 命中数：$($script:fatalLines.Count)"
    ''
    '## 自动证据'
    ''
    '- [ ] samples.csv 全程 Foreground 为 true，或后台时段与人工操作记录一致。'
    '- [ ] fatal-anr.txt 为空。'
    '- [ ] runtime-process.txt 的 MissingSamples 与 RestartCount 都为 0。'
    '- [ ] package-info.txt 的包名、versionName/versionCode 与本次 RC 一致。'
    '- [ ] gfxinfo-framestats.txt 已审核，无持续卡顿尖峰。'
    '- [ ] PSS 无持续失控增长，峰值适合目标低端设备。'
    '- [ ] 电池温度/thermal 状态无危险升温或严重降频。'
    ''
    '## 必须人工填写'
    ''
    "- [ ] 安装模式已核对：$installMode；FreshInstall 会清除本应用旧存档并验证首次初始化。"
    '- [ ] 离线进入、自动战斗、Boss、掉落、装备比较/穿戴和六个底部导航触控正常。'
    '- [ ] 满背包时锁定、已穿戴、Legendary/Mythic 装备不丢失；待领取装备可在腾位后收入。'
    '- [ ] 任务、关卡与设置入口触控正常；Feature Freeze 的邮件、排行、活动不应出现。'
    '- [ ] 切到后台 60 秒再返回，战斗/界面/存档恢复正常。'
    '- [ ] 锁屏 60 秒再解锁，恢复正常且无重复奖励。'
    "- [ ] 连续运行满 $DurationMinutes 分钟，无崩溃、ANR、黑屏或不可恢复卡死。"
    ''
    '人工测试人：'
    ''
    '测试结论（PASS/FAIL）：'
    ''
    '备注：'
) -join [Environment]::NewLine
$template | Set-Content -LiteralPath (Join-Path $evidenceDirectory "ACCEPTANCE.md") -Encoding utf8
Write-Host "Physical-device evidence written to $evidenceDirectory"
if ($script:fatalLines.Count -gt 0) { throw "Physical run captured $($script:fatalLines.Count) FATAL/ANR line(s). See fatal-anr.txt." }
if ($script:processMissingSamples -gt 0) { throw "The app process disappeared during $($script:processMissingSamples) sample(s). See process-observations.csv." }
if ($script:processRestartCount -gt 0) { throw "The app process restarted $($script:processRestartCount) time(s). See process-observations.csv and logcat-stream.txt." }
