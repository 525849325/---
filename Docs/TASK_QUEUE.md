# 无人值守任务队列

状态只使用 `TODO / RUNNING / TESTING / BLOCKED / DONE / FAILED`。每次恢复先选择依赖已满足、可执行的最高优先级任务；外部阻塞不得阻塞其他 Workstream。

| ID | Priority | Dependencies | Status | Owner / Workstream | Result / 下一验收证据 |
|---|---|---|---|---|---|
| GITHUB-SYNC-001 | P0 | — | DONE | Direction/E | `origin/main` 首次 Push 成功；远端 HEAD/SHA 与新鲜克隆核验通过，Unity 必需目录、147 个 `.meta` 与状态文件齐全 |
| AUDIT-001 | P0 | — | DONE | Direction | 审计、资产、冲刺、Backlog、人工事项与 Git 基线完成 |
| CORE-001 | P0 | AUDIT-001 | TESTING | A/B/C | 1 分钟掉落、3 分钟 Boss、换装战力反馈已实现；待 Unity 4 分钟 PlayMode/真人试玩 |
| SAVE-001 | P0 | CORE-001 | TESTING | D | v2 快照、迁移、损坏隔离、离线单次领取已实现；待 Unity 生命周期回归 |
| COMM-001 | P0 | SAVE-001 | TESTING | D/C | 配置商品、首装备后入口、Release 拒绝 Mock、独立设置按钮已实现；待 Unity PlayMode |
| QA-UNITY-001 | P0 | CORE-001,SAVE-001,COMM-001 | BLOCKED | E | Unity Test Runner 在代码加载前被 headless entitlement 拦截（RED-001） |
| BUILD-ANDROID-001 | P0 | QA-UNITY-001 | BLOCKED | E | APK/AAB 脚本与构建规格已完成；实际产物被 RED-001 阻塞 |
| QA-DEVICE-001 | P0 | BUILD-ANDROID-001 | BLOCKED | E | 等待可安装包后执行 10/60 分钟真机测试 |
| SETTINGS-UI-001 | P1 | SAVE-001 | TESTING | C/D | 声音、震动、立即保存、隐私与协议四入口已编译；待当前提交与 Unity 场景运行 |
| FUNNEL-001 | P1 | CORE-001 | DONE | D/E | 无 PII 本地 JSONL 漏斗、会话关联、可替换 Sink 与可执行烟测完成 |
| FEEDBACK-001 | P1 | CORE-001 | TESTING | C | 五类程序化音效已接入且程序集编译通过；待 Unity 场景听感/音量验收 |
| BALANCE-001 | P1 | CORE-STAGE-002 | TODO | A/B/E | 旧脚本只证明独立掉落表；未覆盖 3 分钟后永久 Boss 的真实路径，需重建 10/60 分钟核心循环模拟 |
| WARNINGS-001 | P2 | — | DONE | E | 已迁移 Unity 6 查找与 NamedBuildTarget API；netstandard 2.1 全程序集门禁通过 |
| AUTO-EQUIP-001 | P1 | CORE-001 | TESTING | B/C | 默认开启且可关闭；仅在含功法加成的统一战力严格提升时换装，程序集与领域门禁通过 |
| RC-AUDIT-001 | P0 | CORE-001,SAVE-001,COMM-001 | DONE | Direction/E | P0/P1 静态矩阵完成；修复启动顺序、Feature Freeze 入口与残留 GM UI |
| SAVE-HARDEN-001 | P0 | SAVE-001 | DONE | D/E | 唯一隔离名、不覆盖既有证据、隔离 I/O 失败安全降级；回归测试已编译 |
| INVENTORY-OVERFLOW-001 | P0 | CORE-001 | DONE | B/D/E | 满仓仅回收未穿戴、未锁定、低于 Legendary 的最低价值装备；无安全候选则拒绝收入，领域门禁实际执行通过 |
| IDENTITY-PRIVACY-001 | P1 | COMM-001 | DONE | D/E | 移除设备唯一标识，改为应用随机匿名 ID；服务器入口仅 Development 可见；清空无关 PS4 模板字段 |
| SAVE-AGGREGATE-001 | P0 | SAVE-001 | TODO | D/E | 建立 Stage/Realm/Cultivation/Root/Guide/Task 聚合存档与迁移，覆盖完整成长重载 |
| CORE-STAGE-002 | P0 | CORE-001 | TODO | A/B/E | 修复 3 分钟后永久 Boss、时间推进、失败停战；改为胜利驱动关卡和失败重试 |
| RC-QUALITY-001 | P0 | RC-AUDIT-001 | RUNNING | E | 满仓数据安全与匿名身份已修复；下一步执行聚合进度存档，然后重构胜利驱动核心循环 |

当前自动执行：`RC-QUALITY-001`（P0 数据安全与完成度收口）。若 Unity 授权恢复，立即抢占并执行 `QA-UNITY-001`。
