[CmdletBinding()]
param(
    [string]$AdbPath = "",
    [string]$Serial = "",
    [string]$ApkPath = "",
    [ValidateRange(1, 720)]
    [int]$DurationMinutes = 120,
    [ValidateRange(5, 3600)]
    [int]$SampleIntervalSeconds = 60,
    [switch]$SkipInstall,
    [switch]$DeviceCheckOnly
)

$ErrorActionPreference = "Stop"
$packageName = "com.immortalloot.prototype"
$activityName = "com.unity3d.player.UnityPlayerActivity"
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

Write-Host "Physical Android device accepted: $($properties.Manufacturer) $($properties.Model), Android $($properties.Android) / API $($properties.ApiLevel), serial $($properties.Serial)"
if ($DeviceCheckOnly) { return }

if (-not $ApkPath) { $ApkPath = Join-Path $workspaceRoot "Build\Android\ImmortalLoot-development.apk" }
$resolvedApk = (Resolve-Path -LiteralPath $ApkPath).Path
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$safeSerial = Get-SafeFileName $script:deviceSerial
$evidenceDirectory = Join-Path $workspaceRoot "Docs\Evidence\AndroidPhysical\$timestamp-$safeSerial"
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$properties.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" } | Set-Content -LiteralPath (Join-Path $evidenceDirectory "device-properties.txt") -Encoding utf8

if (-not $SkipInstall) {
    Invoke-Adb -Arguments @("-s", $script:deviceSerial, "install", "-r", $resolvedApk) | Set-Content -LiteralPath (Join-Path $evidenceDirectory "install.txt") -Encoding utf8
}

Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "am", "force-stop", $packageName) | Out-Null
Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "am", "start", "-W", "-n", "$packageName/$activityName") | Set-Content -LiteralPath (Join-Path $evidenceDirectory "launch.txt") -Encoding utf8
Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "gfxinfo", $packageName, "reset") -AllowFailure | Out-Null

$sampleFile = Join-Path $evidenceDirectory "samples.csv"
"UtcTime,ElapsedSeconds,Foreground,PssTotalKb,BatteryLevel,BatteryTemperatureTenthsC" | Set-Content -LiteralPath $sampleFile -Encoding utf8
$started = Get-Date
$deadline = $started.AddMinutes($DurationMinutes)
$sampleIndex = 0
do {
    $elapsed = [int]((Get-Date) - $started).TotalSeconds
    $activity = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "activity", "activities") -AllowFailure
    $foreground = if ($activity -match [regex]::Escape($packageName)) { "true" } else { "false" }
    $meminfo = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "meminfo", $packageName) -AllowFailure
    $pssMatch = [regex]::Match($meminfo, '(?m)^\s*TOTAL\s+(\d+)')
    $pssKb = if ($pssMatch.Success) { $pssMatch.Groups[1].Value } else { "" }
    $battery = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "battery") -AllowFailure
    $levelMatch = [regex]::Match($battery, '(?m)^\s*level:\s*(\d+)')
    $temperatureMatch = [regex]::Match($battery, '(?m)^\s*temperature:\s*(\d+)')
    $level = if ($levelMatch.Success) { $levelMatch.Groups[1].Value } else { "" }
    $temperature = if ($temperatureMatch.Success) { $temperatureMatch.Groups[1].Value } else { "" }
    "$(Get-Date -AsUTC -Format o),$elapsed,$foreground,$pssKb,$level,$temperature" | Add-Content -LiteralPath $sampleFile -Encoding utf8
    $meminfo | Set-Content -LiteralPath (Join-Path $evidenceDirectory ("meminfo-{0:D4}.txt" -f $sampleIndex)) -Encoding utf8
    Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "thermalservice") -AllowFailure | Set-Content -LiteralPath (Join-Path $evidenceDirectory ("thermal-{0:D4}.txt" -f $sampleIndex)) -Encoding utf8
    $sampleIndex++
    if ((Get-Date) -lt $deadline) { Start-Sleep -Seconds $SampleIntervalSeconds }
} while ((Get-Date) -lt $deadline)

Invoke-Adb -Arguments @("-s", $script:deviceSerial, "shell", "dumpsys", "gfxinfo", $packageName, "framestats") -AllowFailure | Set-Content -LiteralPath (Join-Path $evidenceDirectory "gfxinfo-framestats.txt") -Encoding utf8
$logcat = Invoke-Adb -Arguments @("-s", $script:deviceSerial, "logcat", "-d", "-v", "threadtime") -AllowFailure
$logcat | Set-Content -LiteralPath (Join-Path $evidenceDirectory "logcat.txt") -Encoding utf8
$fatalLines = @($logcat -split "`r?`n" | Where-Object { $_ -match 'FATAL EXCEPTION|ANR in com\.immortalloot\.prototype|Fatal signal' })
$fatalLines | Set-Content -LiteralPath (Join-Path $evidenceDirectory "fatal-anr.txt") -Encoding utf8

$template = @"
# ImmortalLoot 低端 Android 物理真机验收

- 设备：$($properties.Manufacturer) $($properties.Model)
- Android / API：$($properties.Android) / $($properties.ApiLevel)
- ABI：$($properties.AbiList)
- 序列号：$($properties.Serial)
- APK：$resolvedApk
- 采集开始：$($started.ToString('o'))
- 计划时长：$DurationMinutes 分钟
- 自动采样数：$sampleIndex
- FATAL/ANR 命中数：$($fatalLines.Count)

## 自动证据

- [ ] `samples.csv` 全程 Foreground 为 true，或后台时段与人工操作记录一致。
- [ ] `fatal-anr.txt` 为空。
- [ ] `gfxinfo-framestats.txt` 已审核，无持续卡顿尖峰。
- [ ] PSS 无持续失控增长，峰值适合目标低端设备。
- [ ] 电池温度/thermal 状态无危险升温或严重降频。

## 必须人工填写

- [ ] 首次安装与冷启动成功。
- [ ] 登录、自动战斗、Boss、掉落、穿戴和六个底部导航触控正常。
- [ ] 任务、邮件、排行、活动与关卡侧栏触控正常。
- [ ] 切到后台 60 秒再返回，战斗/界面/存档恢复正常。
- [ ] 锁屏 60 秒再解锁，恢复正常且无重复奖励。
- [ ] Wi-Fi 断开后请求给出失败反馈；恢复网络后重试不重复发奖。
- [ ] 连续运行满 $DurationMinutes 分钟，无崩溃、ANR、黑屏或不可恢复卡死。

人工测试人：

测试结论（PASS/FAIL）：

备注：
"@
$template | Set-Content -LiteralPath (Join-Path $evidenceDirectory "ACCEPTANCE.md") -Encoding utf8
Write-Host "Physical-device evidence written to $evidenceDirectory"
if ($fatalLines.Count -gt 0) { throw "Physical run captured $($fatalLines.Count) FATAL/ANR line(s). See fatal-anr.txt." }
