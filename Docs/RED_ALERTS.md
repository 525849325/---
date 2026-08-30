# RED Alerts

## RED-002｜GitHub 仓库 URL

- **TASK：** GITHUB-SYNC-001
- **问题：** 当前 Git 配置没有 remote，项目文件中没有仓库地址，本机未安装 GitHub CLI，无法唯一确定已创建仓库。
- **原因：** GitHub 仓库 URL 属于外部资源标识，不能安全猜测。
- **已经尝试：** 检查 `git remote -v`、`.git/config`、项目文档中的 GitHub URL，并检查 GitHub CLI 可用性。
- **推荐方案：** 老板提供已创建仓库的 HTTPS URL，例如 `https://github.com/OWNER/REPO.git`。
- **备选方案：** 提供等价 SSH URL；如果远端已有提交，将先拉取审计，绝不强推覆盖。
- **风险：** URL 未提供前无法配置 remote、Push 或验证远端树；本地提交与安全标签不受影响。
- **最晚需要用户决定：** 首次远端备份与项目监控启用前。
- **状态：** RESOLVED（2026-08-30）；`origin/main` Push 与新鲜克隆文件核验通过。

## RED-001｜Unity Editor/headless entitlement

- **TASK：** QA-UNITY-001 / BUILD-ANDROID-001
- **问题：** 旧批处理会在加载代码前返回 198；人工确认 Hub Personal 有效后需要重新建立自动化证据。
- **原因：** 旧 LicenseClient 会话/许可证缓存未同步；不是用户缺少 Unity 许可证。刷新后本机 entitlement 明确包含 Editor、headless 与 Android。
- **已经尝试：** 对比 Hub 与 batch 日志；确认版本化 LicensingClient 成功解析 Unity Personal；修复真实编译/PlayMode 问题；通过纯 ASCII 临时驱动器绕过 Android 工具的中文路径限制。
- **结果：** EditMode 81/81、PlayMode 5/5；APK/AAB 构建退出码 0并完成产物校验。
- **残余风险：** batchmode 只完成 UI 结构审计，完整 9:16 视觉边界和真机稳定性仍由 QA-DEVICE-001 验收；测试签名不可用于商店发布。
- **状态：** RESOLVED（2026-08-30）；无需用户再次激活许可证。

### 自动化会话补充（2026-08-30）

- Hub 再次成功刷新 Unity Personal seat，许可证本身保持有效。
- 基线门禁已经留下 EditMode 81/81、PlayMode 5/5 与 APK/AAB 成功证据。
- 最新代码回归时，Hub 当前跟踪的是另一项目路径；本项目 batch 无法连接持有 entitlement 的版本化 IPC，独立 LicensingClient 又没有 Hub 会话令牌，因此返回 198。
- 该问题记录为 `QA-REGRESSION-002` 技术阻塞，不重新升级 RED、不要求用户重复登录或激活；其他 P0 继续执行。
- 后续只读复核确认：当前可见交互 Editor 已连接 `LicenseClient-Admin`，但 `Editor.log` 绑定的是另一 Unity 工作区；本项目 `Temp/UnityLockfile` 未被进程持有，不能据此宣称本项目存在可复用的已授权 Editor 会话。
- 这仍是自动化会话/项目绑定差异，不是许可证缺失；未发现需要老板执行的具体账号或权限动作，因此保持无 OPEN RED，并继续处理不依赖 Unity Test Runner 的 P0。
- 最新只读诊断进一步确认 Hub 项目索引当前仅跟踪另一 Unity 工作区，Hub 曾明确拒绝直接打开未登记的本项目路径；同时系统仍存在且可访问已成功跑过基线门禁的版本化管道 `Unity-LicenseClient-Admin-6000.5.10`。
- 自动化脚本已停止把退出码 198 解释为“用户没有许可证”，并改为只在 lock 文件确实被进程持有时阻止运行；下一策略是显式复用该已授权版本化 IPC，不终止现有 Editor/Hub、不删除当前 stale lock、不要求重新激活。
