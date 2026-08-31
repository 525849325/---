# RED Alerts

当前状态：**无 OPEN RED**。Unity Personal 许可证、headless entitlement 与 Android entitlement 均已直接验证有效；不需要用户重复激活、登录或授权。

## RED-001｜Unity Editor 自动化授权链路

- **TASK：** QA-UNITY-POSTCHANGE-002 / BUILD-ANDROID-003
- **初始问题：** batchmode 在加载项目代码前曾返回 entitlement/IPC 错误，而同一用户通过 Hub 可以正常启动 Unity 6000.5.10f1。
- **真实根因：** 不是许可证失效。Unity 6000.5.10f1 的自动化进程需要 Editor 随附 Licensing Client 1.18.3 的版本化管道；Hub 通用 Licensing Client 1.17.4、默认独立启动的用户/管道上下文与该 Editor 协议不一致，可能返回 505 或无法获得对应会话。Android 工具另会拒绝包含非 ASCII 字符的项目路径。
- **恢复方案：** 隐藏启动 Editor 随附的 `Unity.Licensing.Client.exe --namedPipe Unity-LicenseClient-Admin-6000.5.10`，并给 Unity 传入 `-licensingIpc LicenseClient-Admin-6000.5.10`。构建阶段仅临时将项目映射到 ASCII 路径 `R:`；结束前确认无 batchmode Unity 进程并删除映射。
- **直接证据：** 授权日志返回 Personal Unlimited，并授予 headless 与 Android entitlement；当前 `9071636` 的 EditMode 132/132、PlayMode 36/36 均通过；APK 与 AAB 均构建返回 0，且通过包信息、ABI、签名、Manifest 与 bundletool 校验。
- **安全边界：** 未删除或重置许可证缓存，未要求用户重新激活，未结束用户正在使用的其他 Unity Editor，未复用或写入 Hub 会话 Token。临时版本化 Licensing Client 已自行退出，`R:` 映射已清理。
- **后续复用：** 若重启后 batchmode 再次出现授权错误，先检查 Unity 版本、Editor 随附 Licensing Client 版本、版本化 pipe、启动用户、Hub/Unity 冲突进程和完整授权日志；不得机械重复通用 IPC 或把问题重新归因为“用户没有许可证”。
- **风险：** 该恢复依赖 Unity 6000.5.10f1 的版本化 pipe 名称；升级 Editor 时必须同时更新客户端路径与 pipe 版本。此风险可自动诊断，不需要老板当前决策。
- **最晚需要用户决定：** 无。
- **状态：** RESOLVED（2026-08-31）。

## RED-002｜GitHub 仓库 URL

- **TASK：** GITHUB-SYNC-001
- **问题：** 初始本地配置无法唯一确定已创建仓库 URL。
- **结果：** 用户提供 `https://github.com/525849325/---`；`origin` 已配置，默认分支 `main` 已 Push，并以远程 SHA 与 GitHub API 直接验证 Unity 必要目录和日报。
- **状态：** RESOLVED（2026-08-30）。
