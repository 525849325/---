# 无人值守任务队列

状态只使用 `TODO / RUNNING / TESTING / BLOCKED / DONE / FAILED`。每次恢复先选择依赖已满足、可执行的最高优先级任务；外部阻塞不得阻塞其他 Workstream。

| ID | Priority | Dependencies | Status | Owner / Workstream | Result / 下一验收证据 |
|---|---|---|---|---|---|
| GITHUB-SYNC-001 | P0 | — | DONE | Direction/E | `origin/main` 首次 Push 成功；远端 HEAD/SHA 与新鲜克隆核验通过，Unity 必需目录、147 个 `.meta` 与状态文件齐全 |
| AUDIT-001 | P0 | — | DONE | Direction | 审计、资产、冲刺、Backlog、人工事项与 Git 基线完成 |
| CORE-001 | P0 | AUDIT-001 | DONE | A/B/C | 1 分钟掉落、3 分钟 Boss、换装战力反馈及 10/60 分钟自动化门禁通过；真人体验并入 QA-DEVICE-001 |
| SAVE-001 | P0 | CORE-001 | DONE | D | v3 聚合快照、迁移、损坏隔离、离线单次领取及 Unity 生命周期回归通过 |
| COMM-001 | P0 | SAVE-001 | DONE | D/C | 配置商品、首装备后入口、Release 拒绝 Mock、独立设置按钮及 Unity 回归通过；正式商店签名仍属外部发布项 |
| QA-UNITY-001 | P0 | CORE-STAGE-002,SAVE-HARDEN-001,SAVE-AGGREGATE-001 | DONE | E | Unity 6000.5.10f1：显式复用版本化授权 IPC；最终 EditMode 109/109、PlayMode 26/26，日志错误扫描 0 |
| QA-REGRESSION-002 | P0 | QA-UNITY-001 | DONE | E | 三轮独立审查发现项均修复；后端并发/旧库升级 Verification 与全部离线质量门禁 PASS；UI 仅结构审计，不冒充视觉验收 |
| CORE-LOGIN-GATE-001 | P0 | SAVE-HARDEN-001,SAVE-AGGREGATE-001,QA-UNITY-001 | DONE | D/E | 登录前不读取离线存档；服务器 profile/inventory 校验后才进入；服务器结算/暂停/退出不写本地存档；权威关卡与奖励回归通过 |
| SERVER-PROFILE-RECONCILE-001 | P1 | CORE-LOGIN-GATE-001 | DONE | D/E | profile 刷新严格校验 current/cleared 契约并原子重建关卡镜像；RED→GREEN 1/1，完整 PlayMode 26/26 |
| SERVER-BATTLE-AUTH-001 | P1 | SERVER-PROFILE-RECONCILE-001 | DONE | D/E | canonical/exact-current 开战、单活动会话、同键/异键并发合并、丢失响应恢复、过期 Boss 零副作用与旧 SQLite schema 升级全部通过 Backend Verification；检查点 `4a28d4c` |
| SERVER-HTTP-CONTRACT-001 | P1 | SERVER-BATTLE-AUTH-001 | DONE | D/E | 真实 Kestrel HTTP 39/39：401/400/409、鉴权、profile/inventory、Start 恢复、Finish 幂等/欠战力拒绝与权威推进通过；500→409 RED→GREEN；检查点 `b7ea774` |
| BUILD-ANDROID-001 | P0 | QA-UNITY-001 | DONE | E | 基线 `060f4df` APK/AAB 已通过 badging/zipalign/v2/bundletool；仅作为历史可构建证据 |
| BUILD-ANDROID-002 | P0 | QA-REGRESSION-002,CORE-LOGIN-GATE-001,SERVER-PROFILE-RECONCILE-001 | DONE | E | 提交 `2bee3ac` 的 APK/AAB 构建均 exit 0；APK `F5AEBAD4...168C` 通过 metadata/zipalign/v2，AAB `E4633FC0...EE30` 通过 bundletool/Manifest/双 ABI 门禁 |
| QA-DEVICE-001 | P0 | BUILD-ANDROID-002 | BLOCKED | E | 最新 APK 已就绪；DeviceCheckOnly 实测 ADB daemon 正常但 0 台已授权物理设备，未安装 APK；设备可用后执行 10/60 分钟、视觉与触控验收 |
| SETTINGS-UI-001 | P1 | SAVE-001 | DONE | C/D | 声音、震动、立即保存、隐私与协议四入口已通过实际 Unity EditMode/PlayMode；真机触控观感并入 QA-DEVICE-001 |
| FUNNEL-001 | P1 | CORE-001 | DONE | D/E | 无 PII 本地 JSONL 漏斗、会话关联、可替换 Sink 与可执行烟测完成 |
| FEEDBACK-001 | P1 | CORE-001 | TESTING | C | 五类程序化音效已接入且程序集编译通过；待 Unity 场景听感/音量验收 |
| BALANCE-001 | P1 | CORE-STAGE-002 | TESTING | A/B/E | 生产战斗引擎 10/60 分钟离线门禁 PASS：首 Boss 182.23s 到达、193.43s 击败，Boss 胜利 21/233，奖励 22/142 全消费且无卡死；待真机长测 |
| WARNINGS-001 | P2 | — | DONE | E | 已迁移 Unity 6 查找与 NamedBuildTarget API；netstandard 2.1 全程序集门禁通过 |
| AUTO-EQUIP-001 | P1 | CORE-001 | DONE | B/C | 默认开启且可关闭；仅在含功法加成的统一战力严格提升时换装，实际 Unity、程序集与领域门禁通过 |
| RC-AUDIT-001 | P0 | CORE-001,SAVE-001,COMM-001 | DONE | Direction/E | P0/P1 静态矩阵完成；修复启动顺序、Feature Freeze 入口与残留 GM UI |
| SAVE-HARDEN-001 | P0 | SAVE-001 | DONE | D/E | 唯一隔离名、不覆盖既有证据、隔离 I/O 失败安全降级；回归测试已编译 |
| INVENTORY-OVERFLOW-001 | P0 | CORE-001 | DONE | B/D/E | 持久化单槽、窗口跳过、单次领取与二次确认替换均通过最新 EditMode/PlayMode；伪 pending 序列化回归已修复 |
| IDENTITY-PRIVACY-001 | P1 | COMM-001 | DONE | D/E | 移除设备唯一标识，改为应用随机匿名 ID；服务器入口仅 Development 可见；清空无关 PS4 模板字段 |
| SAVE-AGGREGATE-001 | P0 | SAVE-001 | DONE | D/E | v3 聚合存档、v1/v2 迁移及阶段/境界/功法/灵根/引导/任务恢复通过最新 EditMode/PlayMode；空壳对象归一化已覆盖 |
| CORE-STAGE-002 | P0 | CORE-001 | DONE | A/B/E | 胜利推进、失败重试、Boss 回环、奖励窗口与在线无奖通关契约通过实际 Unity、核心离线及后端门禁 |
| RC-QUALITY-001 | P0 | RC-AUDIT-001 | TESTING | E | Unity RC `2bee3ac`、后端 `4a28d4c`、HTTP 门禁 `b7ea774`、Edit 109/109、Play 26/26、真实 HTTP 39/39 与离线回归全绿；物理设备验收单独 BLOCKED |

当前自动执行：`RC-QUALITY-001`（继续不依赖真机的残余 P2 鲁棒性审计；设备可用时自动恢复 QA-DEVICE-001）。
