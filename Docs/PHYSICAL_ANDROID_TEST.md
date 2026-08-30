# 低端 Android 物理真机验收

该门槛只能由物理安卓设备完成。脚本会在安装 APK 之前检查 `ro.kernel.qemu`、序列号、硬件与厂商品牌，并拒绝夜神、Android Emulator、VirtualBox、Genymotion 等环境，防止模拟器证据被误计为真机证据。

## 准备

1. 在手机开启开发者选项与 USB 调试，使用数据线连接并在手机上允许本机调试。
2. 断开夜神等模拟器，或通过 `-Serial` 明确选择物理设备。
3. 确认 `Build/Android/ImmortalLoot-development.apk` 是待验收版本。

只检查设备是否为物理设备，不安装或启动：

```powershell
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -DeviceCheckOnly
```

执行标准 120 分钟采集：

```powershell
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -DurationMinutes 120 -SampleIntervalSeconds 60
```

多设备连接时：

```powershell
& '.\Tools\Android\Invoke-PhysicalDeviceAcceptance.ps1' -Serial '<adb serial>' -DurationMinutes 120
```

脚本会安装/覆盖 APK、强制停止并启动游戏，然后写入 `Docs/Evidence/AndroidPhysical/<时间>-<序列号>/`：设备属性、安装与启动结果、逐分钟 PSS/电量/温度/前台状态、thermal、gfx framestats、完整 logcat、FATAL/ANR 摘要及人工验收表。

## 通过条件

- 物理设备检查通过，设备确属目标低端机型。
- 连续运行满 120 分钟，无崩溃、ANR、黑屏或不可恢复卡死。
- 内存没有持续失控增长；温度、耗电和降频表现可接受。
- 登录、战斗、Boss、掉落、穿戴、全部导航触控正常。
- 后台/前台与锁屏恢复正常。
- 断网有明确失败反馈；恢复后重试不会重复发放奖励。
- `ACCEPTANCE.md` 的人工项全部填写并由测试人给出 PASS。

如果未来重新把物理真机认证纳入发布范围，只有完整证据目录和人工结论同时存在，才能把 `MVP_ACCEPTANCE.md` 的“低端 Android 物理真机”从 `WAIVED` 升级为 `PASS`。
