# Project ImmortalLoot / 《太初：无尽轮回》

原创修仙放置刷宝手游 MVP。Phase 1–20 已完成：核心刷宝循环、随机装备词条、挂机、境界渡劫、九系灵根、火/雷/血三套功法 Build、任务活动邮件，以及含鉴权、商城、Development-only Mock 支付和三维周期排行榜的权威后端均已落地。Android 功能与兼容验收使用夜神 Android 7.1.2；按本次 MVP 的用户批准范围，不包含物理真机发布认证。

## 运行

1. 使用 Unity `6000.5.10f1` 打开仓库根目录。
2. 打开 `Assets/Game/Scenes/Main.unity` 并进入 Play Mode。
3. 若需要重建场景，执行菜单 `ImmortalLoot > Build Prototype Scene`。

Player 构建入口位于 `ImmortalLoot > Build`：Windows、WebGL、Android Development APK，以及 Android Release Candidate APK/AAB。Android 产物使用版本化文件名输出到 `Build/Android/`；当前 `0.1.0` 对应：

- `Taichu-Endless-Reincarnation-0.1.0-dev.apk`
- `Taichu-Endless-Reincarnation-0.1.0-rc.apk`
- `Taichu-Endless-Reincarnation-0.1.0-rc.aab`

无人值守 RC APK 命令：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' -batchmode -quit -projectPath . -executeMethod ImmortalLoot.Editor.PlayerBuildTools.BuildAndroidReleaseCandidateApk -logFile Logs/android-rc-build.log
```

未配置自定义 keystore 时，RC 仅使用 Unity 默认测试签名，适合安装验证但不能作为商店正式签名包；所需人工操作见 `Docs/HUMAN_ACTION_REQUIRED.md`。

在线模式需先在另一个 PowerShell 启动开发服务器：

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
& 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\DotNetSdk\dotnet.exe' run --project '.\Backend\src\ImmortalLoot.Server\ImmortalLoot.Server.csproj'
```

随后点击登录页的服务器登录按钮。Development 环境允许商城按钮演示“服务器建单 → Mock Provider 回执 → 服务器验证 → 发放权益”；Production 环境始终拒绝 Mock 回执。

## 测试

在 Unity 中打开 `Window > General > Test Runner`，运行 EditMode 与 PlayMode 测试。当前机器需要普通 Editor 授权路径，命令如下：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml
& 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe' -projectPath . -runTests -testPlatform PlayMode -testResults PlayModeTestResults.xml
& '.\Tools\Verification\compile-unity-assemblies.ps1'
& '.\Tools\Verification\run-domain-tests.ps1'
& '.\Tools\Verification\run-balance-simulation.ps1'
```

## 架构

- `Core`：可替换的随机源等基础抽象。
- `Battle`：不依赖动画的纯 C# 自动战斗模拟。
- `Equipment`：装备领域模型和通用加权词条生成器。
- `UI`：离线演示驱动本地域模型；服务器登录后自动战斗结算、装备同步与各功能页调用权威 REST。
- `Config`：统一配置源、JSON 仓库、版本和引用校验；装备与词条已接入。
- `Tests/EditMode`：核心逻辑的快速单元测试。
- `Character`：完整角色属性、来源解耦的修改器和统一聚合服务。
- `Backend`：ASP.NET Core 8、EF Core、游客鉴权、服务器权威奖励事务、幂等流水和环境隔离支付验证。

详尽阶段状态见 `Docs/PHASE_STATUS.md`。

## 无人值守开发

项目已内置“需求 → Codex 开发 → Unity/后端测试 → 自动截图 → UI 审查 → 自动修复 → 构建 → Git 留档”的可复用入口。使用方式和安全边界见 `Tools/Autonomous/README.md`。日常完整质量门禁可直接运行：

```powershell
.\Tools\Autonomous\Invoke-QualityGate.ps1
```

给出新需求并让 Codex 自主循环：

```powershell
.\Tools\Autonomous\Invoke-AutonomousDev.ps1 -Requirement "你的新需求"
```
120 分钟人工体验流程与 Development Player 自动采样证据见 `Docs/TWO_HOUR_PLAYTEST.md`。
Player 默认按 `demo_pacing.json` 以真实时间推进。Development QA 可用 `-playtestSpeed=120 -playtestAutoQuit` 执行加速无图形 soak；非 Development 构建忽略这些参数，普通玩家流程不会加速。

低端 Android 物理真机发布验收见 `Docs/PHYSICAL_ANDROID_TEST.md`。`Tools/Android/Invoke-PhysicalDeviceAcceptance.ps1` 会先拒绝模拟器，再执行标准 120 分钟 ADB 指标采集并生成待人工签署的证据目录。
