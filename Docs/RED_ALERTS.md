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
- **状态：** OPEN；GITHUB-SYNC-001 BLOCKED，RC-QUALITY-001 继续执行。

## RED-001｜Unity Editor/headless entitlement

- **TASK：** QA-UNITY-001 / BUILD-ANDROID-001
- **问题：** Unity 6000.5.10f1 batch Test Runner 和 Android 构建在加载项目代码前退出，返回码 198。
- **原因：** 当前登录环境缺少 `com.unity.editor.headless` entitlement / 有效 Editor 许可证。
- **已经尝试：** EditMode、PlayMode、Android RC 构建入口均复现；完整 Runtime/Editor/测试程序集改用 Unity 自带编译器独立编译；领域与后端验证继续运行。
- **推荐方案：** 在 Unity Hub 登录具备 Unity 6 Editor 权限的账号并激活许可证，然后重跑质量门禁。
- **备选方案：** 在另一台已授权机器或已配置许可证的 CI Runner 执行相同 Test Runner 与 Android 构建命令。
- **风险：** 无法证明真实场景运行、Console 零错误、APK/AAB 可安装及真机稳定性；GATE 4 不可关闭。
- **最晚需要用户决定：** Android RC 冻结前，建议立即处理。
- **状态：** OPEN；相关任务 BLOCKED，其他任务继续。
