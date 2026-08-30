# Unity 无人值守开发模板

这套流水线把需求开发、Unity 测试、截图、UI 审查、构建和 Git 留档串成一个可重复执行的闭环。

## 一键使用

在仓库根目录运行：

```powershell
.\Tools\Autonomous\Invoke-AutonomousDev.ps1 -Requirement "实现新的玩法需求，并保持现有功能可用"
```

脚本会启动一个非交互 Codex 开发回合。Codex 必须反复调用内层验收命令，修复失败项，并且只有在全部门禁通过后才允许提交 Git：

```powershell
.\Tools\Autonomous\Invoke-QualityGate.ps1
```

快速开发时可跳过 Player 构建：

```powershell
.\Tools\Autonomous\Invoke-QualityGate.ps1 -SkipBuild
```

## 产物

- `TestResults/Autonomous/latest.json`：机器可读的最终结果。
- `TestResults/Autonomous/<run-id>/`：每轮日志和结果快照。
- `TestResults/UI/`：登录、战斗和主要页面截图。
- `TestResults/UI/ui-audit.json`：分辨率、越界、文字裁切、按钮尺寸和重叠检查。

## 复用到新项目

复制 `Tools/Autonomous` 和 `Assets/Game/Tests/PlayMode/AutonomousUiAcceptanceTests.cs`，然后修改 `pipeline.json` 中的 Unity 版本、场景、构建入口和页面按钮。新项目必须让所有 UI 关键页都进入截图清单，不能仅靠单元测试代替视觉验收。

## 安全边界

- 自动化只修改当前仓库，不推送远端，不发布商店，不处理真实支付密钥。
- Git 仅在门禁通过后提交；已有的用户改动不会被重置或丢弃。
- 连续失败会以非零退出码停止，并保留证据供下一次 Codex 回合继续。
