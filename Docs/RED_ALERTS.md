# RED Alerts

当前状态：**无 OPEN RED**。

## RED-001｜Unity Editor/headless entitlement

- **TASK：** QA-UNITY-001 / BUILD-ANDROID-001
- **问题：** 早期 batchmode 在加载项目代码前返回 198，而同用户通过 Hub 启动 Editor 正常。
- **真实原因：** Hub 启动的 Editor 自动携带 Hub 会话；受限 batch 环境未自动连接当前版本的授权 IPC。许可证本身、Personal seat、headless 与 Android entitlement 均有效。
- **已经尝试：** 对比 Hub/Editor/batch 日志；检查启动用户、进程冲突、项目锁、许可证缓存与命令行；停止重复无效命令；在同一 Windows 用户上下文显式传入版本化 IPC `LicenseClient-Admin-6000.5.10`。
- **结果：** EditMode 109/109、PlayMode 24/24、APK 与 AAB 构建全部 exit 0；日志确认成功连接 IPC 并解析 entitlement。batch 没有 Hub access token 用于在线刷新，但本地 entitlement 可正常解析，不构成失败。
- **推荐方案：** 自动化继续复用版本化 IPC；不终止用户的 Hub/Editor，不删除许可证缓存，不要求重复激活。
- **备选方案：** 若版本升级导致 IPC 名变化，先只读枚举同版本 LicensingClient/日志，再使用当前用户上下文启动；禁止机械重复旧命令。
- **风险：** 完整视觉/真机稳定性仍属于 QA-DEVICE-001；测试签名不可用于商店发布。
- **最晚需要用户决定：** 无；当前不需要账号或权限动作。
- **状态：** RESOLVED（2026-08-30）。

## RED-002｜GitHub 仓库 URL

- **TASK：** GITHUB-SYNC-001
- **问题：** 初始本地配置无法唯一确定已创建仓库 URL。
- **结果：** 用户提供 `https://github.com/525849325/---`；`origin/main` 已配置、Push 并直接验证。
- **状态：** RESOLVED（2026-08-30）。
