# RED Alerts

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
