# 无人值守任务队列

状态只使用 `TODO / RUNNING / TESTING / BLOCKED / DONE / FAILED`。每次恢复先选择依赖已满足、可执行的最高优先级任务；外部阻塞不得阻塞其他 Workstream。

| ID | Priority | Dependencies | Status | Owner / Workstream | Result / 下一验收证据 |
|---|---|---|---|---|---|
| GITHUB-SYNC-001 | P0 | — | DONE | Direction/E | `origin/main` 首次 Push 成功；远端 HEAD/SHA 与新鲜克隆核验通过，Unity 必需目录、147 个 `.meta` 与状态文件齐全 |
| AUDIT-001 | P0 | — | DONE | Direction | 审计、资产、冲刺、Backlog、人工事项与 Git 基线完成 |
| CORE-001 | P0 | AUDIT-001 | TESTING | A/B/C | 1 分钟掉落、3 分钟 Boss、换装战力反馈已实现；待 Unity 4 分钟 PlayMode/真人试玩 |
| SAVE-001 | P0 | CORE-001 | TESTING | D | v2 快照、迁移、损坏隔离、离线单次领取已实现；待 Unity 生命周期回归 |
| COMM-001 | P0 | SAVE-001 | TESTING | D/C | 配置商品、首装备后入口、Release 拒绝 Mock、独立设置按钮已实现；待 Unity PlayMode |
| QA-UNITY-001 | P0 | CORE-001,SAVE-001,COMM-001 | DONE | E | Unity 6000.5.10f1：EditMode 81/81、PlayMode 5/5；batch UI 结构审计 PASS，完整视觉验收未冒充已完成 |
| QA-REGRESSION-002 | P0 | QA-UNITY-001 | BLOCKED | E | 最新背包 P0 全程序集编译 PASS；Hub seat 有效，但当前项目 batch 无法取得已授权版本化 IPC，会话修复后验收最新实现 |
| BUILD-ANDROID-001 | P0 | QA-UNITY-001 | DONE | E | 基线 `060f4df` APK/AAB 已通过 badging/zipalign/v2/bundletool；仅作为历史可构建证据 |
| BUILD-ANDROID-002 | P0 | QA-REGRESSION-002 | BLOCKED | E | 最新 HEAD 必须先通过 Unity 回归再重建 APK/AAB；当前基线包不得用于最新代码验收 |
| QA-DEVICE-001 | P0 | BUILD-ANDROID-002 | BLOCKED | E | 脚本/清单/证据链已准备；最新 APK 尚未生成且 ADB 当前为 0 台已授权物理设备，二者解除后执行 10/60 分钟验收 |
| SETTINGS-UI-001 | P1 | SAVE-001 | TESTING | C/D | 声音、震动、立即保存、隐私与协议四入口已编译；待当前提交与 Unity 场景运行 |
| FUNNEL-001 | P1 | CORE-001 | DONE | D/E | 无 PII 本地 JSONL 漏斗、会话关联、可替换 Sink 与可执行烟测完成 |
| FEEDBACK-001 | P1 | CORE-001 | TESTING | C | 五类程序化音效已接入且程序集编译通过；待 Unity 场景听感/音量验收 |
| BALANCE-001 | P1 | CORE-STAGE-002 | TODO | A/B/E | 旧脚本只证明独立掉落表；未覆盖 3 分钟后永久 Boss 的真实路径，需重建 10/60 分钟核心循环模拟 |
| WARNINGS-001 | P2 | — | DONE | E | 已迁移 Unity 6 查找与 NamedBuildTarget API；netstandard 2.1 全程序集门禁通过 |
| AUTO-EQUIP-001 | P1 | CORE-001 | TESTING | B/C | 默认开启且可关闭；仅在含功法加成的统一战力严格提升时换装，程序集与领域门禁通过 |
| RC-AUDIT-001 | P0 | CORE-001,SAVE-001,COMM-001 | DONE | Direction/E | P0/P1 静态矩阵完成；修复启动顺序、Feature Freeze 入口与残留 GM UI |
| SAVE-HARDEN-001 | P0 | SAVE-001 | DONE | D/E | 唯一隔离名、不覆盖既有证据、隔离 I/O 失败安全降级；回归测试已编译 |
| INVENTORY-OVERFLOW-001 | P0 | CORE-001 | TESTING | B/D/E | 真实满仓掉落进入持久化单槽；窗口明确跳过；安全腾位单次领取，升级才二次确认牺牲替换，非升级二次确认分解 pending；Edit/Play 测试已编译，实际 Unity 回归由 QA-REGRESSION-002 阻塞 |
| IDENTITY-PRIVACY-001 | P1 | COMM-001 | DONE | D/E | 移除设备唯一标识，改为应用随机匿名 ID；服务器入口仅 Development 可见；清空无关 PS4 模板字段 |
| SAVE-AGGREGATE-001 | P0 | SAVE-001 | RUNNING | D/E | 建立 Stage/Realm/Cultivation/Root/Guide/Task 聚合存档与迁移，覆盖完整成长重载 |
| CORE-STAGE-002 | P0 | CORE-001 | TODO | A/B/E | 修复 3 分钟后永久 Boss、时间推进、失败停战；改为胜利驱动关卡和失败重试 |
| RC-QUALITY-001 | P0 | RC-AUDIT-001 | RUNNING | E | 背包 incoming 保全进入 TESTING；自动切换聚合存档，Unity 会话恢复后抢占执行回归与 Android 重构建 |

当前自动执行：`SAVE-AGGREGATE-001`（P0 聚合成长进度存档）。
