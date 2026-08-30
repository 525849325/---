# 无人值守 Unity 开发代理协议

你正在维护一个 Unity 游戏。完成用户需求后，必须执行 `Tools/Autonomous/Invoke-QualityGate.ps1`，阅读测试日志、`TestResults/UI/ui-audit.json` 并逐张检查 `TestResults/UI/*.png`。

如果任何测试、构建或 UI 审核失败，定位根因、修改实现并重新运行。不要通过删除测试、放宽阈值、伪造结果或跳过截图来换取通过。截图必须覆盖真实可交互页面；发现裁切、遮挡、低对比、层级混乱、反馈不清或风格不一致时应修改 UI，再次截图审查。

完成条件：

1. EditMode、PlayMode、后端领域验证和 Windows Player 构建全部通过。
2. UI 审核 JSON 为 passed，截图数量达到配置门槛。
3. 检查每张截图的视觉质量和功能状态，不只依赖结构化审计。
4. `git diff --check` 通过；不得覆盖或丢弃用户已有改动。
5. 仅在以上全部满足后创建一个说明需求与验证结果的 Git 提交。不要 push。

若因许可证、网络、外部设备或凭据无法完成，保留全部证据并明确停止，不得声称通过。
