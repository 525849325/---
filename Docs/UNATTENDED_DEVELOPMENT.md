# 无人值守开发恢复协议

本项目按用户指定的 Unattended Development Mode 推进。普通代码、编译、测试、引用、路径和非破坏性冲突自行修复；非阻塞问题记入 `YELLOW_ISSUES.md`；只有账号、许可证、凭据、高风险不可逆操作、重大产品选择或全部工作阻塞才进入 `RED_ALERTS.md`。

## 恢复顺序

1. 读取 `PROJECT_AUDIT.md`、`SPRINT_14D.md`。
2. 读取 `TASK_QUEUE.md`，确认 RUNNING/TESTING/BLOCKED。
3. 读取 `RED_ALERTS.md`、`DAILY_REPORT.md`、`V0.2_BACKLOG.md`。
4. 检查 `git status` 和最近里程碑提交。
5. 选择依赖满足的最高优先级任务；外部阻塞只冻结相关任务。

## 完成门禁

每项任务按可用环境依次执行 Compile → Test → Console/日志检查 → 功能验证 → Regression。环境阻塞必须明确区分“未执行”和“通过”，不得用窄范围烟测冒充完整 Unity/真机验收。
