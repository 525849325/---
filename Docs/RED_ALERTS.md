# RED Alerts

当前状态：**无 OPEN RED**。Unity Personal 许可证和 Hub 人工启动已由用户验证有效；当前阻塞不是“用户没有许可证”，也没有需要用户重复激活、登录或授权的具体动作。

## RED-001｜Unity Editor 自动化授权链路

- **TASK：** QA-UNITY-POSTCHANGE-002 / BUILD-ANDROID-003
- **初始问题：** 早期 batchmode 在加载项目代码前返回 entitlement 错误，而同一用户通过 Hub 可正常启动 Editor。
- **历史恢复：** 在源码检查点 `2bee3ac`，版本化 Licensing IPC 曾让 EditMode 109/109、PlayMode 26/26、APK 与 AAB 构建全部 exit 0。这证明 Personal seat、Editor、headless 与 Android entitlement 当时有效，但该证据不自动适用于当前源码。
- **当前复核：** 再次只读核对后，Hub 授权的主 Editor 仍实际打开 `C:\Users\Admin\Documents\Codex\2026-08-28\apk-unity-x20\HuanLingXiuXian`，不是本仓库；目标项目只有旧 lock、没有 EditorInstance，仍无可复用会话。
- **已经尝试：** 只读检查启动用户、Unity/Hub/Licensing 进程、项目路径、锁和日志；分别尝试版本化 IPC 与通用 Hub IPC；尝试带安全软件条款参数的直接交互启动；尝试 GUI 替代入口。IPC 路径均拒绝或超时，直接启动停在 Software Terms，GUI 不能可靠定位。所有遗留尝试进程均安全停止，未删除许可证缓存、未结束用户正在使用的 Editor，也未复用或写入会话 Token。
- **环境差异：** Hub 正常启动的 Editor 具有 Hub 会话绑定；当前目标仓库的独立 batch/交互进程没有取得同一授权上下文。当前活动授权 Editor 又被另一项目占用，因此无法直接复用到本项目。
- **推荐方案：** 保留用户当前 Hub/Editor 状态；后续目标项目出现可复用授权 Editor/Licensing 会话时，先运行当前 EditMode 112 与 PlayMode 32，再重建 APK/AAB。不得机械重复已失败的两种 IPC 命令。
- **备选方案：** 使用 Unity 官方支持的直接 Editor 命令行或已授权 Editor 内部菜单触发自动化；只在可验证目标项目上下文后执行，避免错误项目被测试或构建。
- **风险：** `ca495ef` 的境界/真实战斗恢复闭环尚无当前 Unity 运行证据；`2bee3ac` 的 APK/AAB 已过期，不能作为当前 RC。
- **最晚需要用户决定：** 无；当前没有具体账号、权限或许可证动作。
- **状态：** RESOLVED（许可证/授权能力已证明）；`QA-UNITY-POSTCHANGE-002` 作为非 RED 环境阻塞保持 BLOCKED，其他任务继续。

## RED-002｜GitHub 仓库 URL

- **TASK：** GITHUB-SYNC-001
- **问题：** 初始本地配置无法唯一确定已创建仓库 URL。
- **结果：** 用户提供 `https://github.com/525849325/---`；`origin/main` 已配置、Push 并直接验证。
- **状态：** RESOLVED（2026-08-30）。
