# Android RC 物理真机验收

该门禁只接受物理 Android 设备。脚本会拒绝夜神、Android Emulator、VirtualBox、Genymotion 等环境，并要求 API 26 或更高。

## 准备

1. 手机开启开发者选项与 USB 调试，使用数据线连接，并在手机上允许本机调试。
2. 断开模拟器；多设备并存时用 `-Serial` 指定物理设备。
3. 默认验收包为 `Build/Android/Taichu-Endless-Reincarnation-0.1.0-rc.apk`。
4. AAB 不能直接安装；其 bundletool 校验独立执行，真机验收使用同版本 APK。
5. 使用 PowerShell 7 (`pwsh`)；脚本明确要求 7.0 或更高版本。

只检查设备，不安装或启动：

```powershell
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -DeviceCheckOnly
```

先执行 10 分钟冒烟，再执行 60 分钟稳定性验收；RC 冻结前可追加 120 分钟浸泡：

```powershell
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -FreshInstall -DurationMinutes 10 -SampleIntervalSeconds 30
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -DurationMinutes 60 -SampleIntervalSeconds 60
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -DurationMinutes 120 -SampleIntervalSeconds 60
```

多设备连接时：

```powershell
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -Serial '<adb serial>' -DurationMinutes 60
```

## 自动证据

脚本会在启动前清空 logcat，并写入 `Docs/Evidence/AndroidPhysical/<时间>-<序列号>/`：

- 设备/API/ABI 与物理机判定属性；
- APK 路径、字节数和 SHA-256；
- 安装、启动、强制 RC 版本匹配及全程应用 PID；
- 前台状态、PSS、电量、温度与 thermal 采样；
- 独立连续采集完整 logcat，避免长测环形缓冲覆盖和读取/清空竞态，并检测本包 FATAL/ANR；
- gfx framestats、进程消失/重启计数；
- 人工验收表 `ACCEPTANCE.md`。

原始真机证据可能包含设备序列号及同机其他应用日志，`Docs/Evidence/` 已默认忽略，禁止直接提交到公开仓库。Dropbox 历史记录不自动导出；如需升级诊断，仅在本地按本包和测试时间过滤后审阅。

RC 验收每次都强制安装指定 APK；`-SkipInstall` 会被拒绝，防止同版本号的旧二进制被误验收。10 分钟冒烟使用显式 `-FreshInstall`，它只卸载本项目包 `com.immortalloot.prototype`、清除该应用本地存档后再安装，用于验证首次初始化；后续 60/120 分钟默认覆盖安装并保留冒烟后的应用数据。

## 通过条件

- 物理设备检查通过，安装版本与本次 RC 一致。
- 离线进入、自动战斗、Boss、掉落、比较/穿戴、六个底部导航可用。
- 满背包时锁定、已穿戴、Legendary/Mythic 装备不被自动丢弃；待领取装备可安全腾位后领取；全保护仓时只在升级情况下二次确认牺牲最低价值未穿戴旧装备，非升级则二次确认分解待领取装备并保留旧装备。
- 任务、关卡与设置可用；Feature Freeze 的邮件、排行、活动不出现。
- 后台/前台、锁屏恢复和重启存档恢复正常，无重复奖励。
- `fatal-anr.txt` 为空，进程全程存在且未重启；内存无持续失控增长，温度/耗电/降频可接受。
- 批处理 UI 测试只证明结构门禁；真机必须人工确认 9:16 边界、文字、触控与视觉表现。
- `ACCEPTANCE.md` 人工项全部填写并给出 PASS。

没有已授权物理设备时，任务保持 `BLOCKED` 并继续其他开发，不升级许可证或账号 RED。
