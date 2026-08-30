# 无人值守任务队列

状态只使用 `TODO / RUNNING / TESTING / BLOCKED / DONE / FAILED`。自动选择依赖已满足的最高优先级任务；外部阻塞不得停止其他 Workstream。

| ID | Priority | Dependencies | Status | Owner / Workstream | Result / 下一验收证据 |
|---|---|---|---|---|---|
| GITHUB-SYNC-001 | P0 | — | DONE | Direction/E | `origin/main` 与 GitHub `525849325/---` 已建立并持续 Push；远端主分支含 Unity 必需目录与 Docs |
| AUDIT-001 | P0 | — | DONE | Direction | 项目审计、冲刺、Backlog、任务/RED/日报机制已建立 |
| CORE-001 | P0 | AUDIT-001 | DONE | A/B/C | 自动战斗、掉落、Boss、换装战力和 10/60 分钟核心循环基线通过 |
| CORE-STAGE-002 | P0 | CORE-001 | DONE | A/B/E | 胜利推进、失败重试、Boss 回环与奖励窗口离线/Unity/后端基线完成 |
| SAVE-001 | P0 | CORE-001 | DONE | D | v3 聚合存档、原子写入、迁移、损坏隔离与离线单次领取完成 |
| SAVE-HARDEN-001 | P0 | SAVE-001 | DONE | D/E | 唯一隔离名、I/O 安全降级与损坏存档延迟读取完成 |
| SAVE-AGGREGATE-001 | P0 | SAVE-001 | DONE | D/E | Stage/Realm/Cultivation/Root/Guide/Task 聚合恢复完成；`bffd195` 新增累计修为向后兼容迁移 |
| COMM-001 | P0 | SAVE-001 | DONE | D/C | 商品结构、成长后曝光、Release 拒绝 Mock 与设置入口完成；真实渠道仍属外部发布项 |
| CORE-LOGIN-GATE-001 | P0 | SAVE-AGGREGATE-001 | DONE | D/E | 本地/服务器状态隔离和登录门禁完成 |
| SERVER-PROFILE-RECONCILE-001 | P1 | CORE-LOGIN-GATE-001 | DONE | D/E | 权威 profile/current-stage 镜像校正完成 |
| SERVER-BATTLE-AUTH-001 | P1 | SERVER-PROFILE-RECONCILE-001 | DONE | D/E | exact-current、单活动会话、并发幂等、失联恢复与旧 SQLite 升级通过 |
| SERVER-HTTP-CONTRACT-001 | P1 | SERVER-BATTLE-AUTH-001 | DONE | D/E | 真实 Kestrel HTTP 46/46 通过；含累计修为、破境石/pending profile 与拒绝零副作用 |
| QA-UNITY-001 | P0 | CORE-STAGE-002,SAVE-AGGREGATE-001 | DONE | E | 历史检查点 `2bee3ac`：EditMode 109/109、PlayMode 26/26；不代表当前源码 |
| BUILD-ANDROID-002 | P0 | QA-UNITY-001 | DONE | E | 历史 `2bee3ac` APK/AAB 曾通过静态产物门禁；当前已 STALE |
| CORE-REALM-INTEGRATION-001 | P0 | CORE-STAGE-002,SAVE-AGGREGATE-001 | TESTING | B/D/E | `ca495ef`：离线修为/Boss/渡劫/灵根及在线破境石/pending→Boss 权威镜像已实现；静态编译、离线 smoke、Backend、HTTP 46/46 和双重审查 PASS，待 Unity 112/32 |
| QA-UNITY-POSTCHANGE-002 | P0 | CORE-REALM-INTEGRATION-001 | BLOCKED | E | 当前授权 Editor 仍打开另一项目；目标项目无 EditorInstance，既有 IPC/交互替代不可复用；无账号动作、非 RED；待跑 EditMode 112 / PlayMode 32 |
| BUILD-ANDROID-003 | P0 | QA-UNITY-POSTCHANGE-002 | BLOCKED | E | 必须在当前 Unity 112/32 全绿后重建 `ca495ef` 对应 APK 与 AAB 并重跑签名/ABI/Manifest 门禁 |
| QA-UI-PORTRAIT-001 | P0 | QA-UNITY-POSTCHANGE-002 | BLOCKED | C/E | 生成真正 1080×1920 九页截图，隔离 run 证据，使非 batch UI audit 完整通过 |
| QA-DEVICE-001 | P0 | BUILD-ANDROID-003,QA-UI-PORTRAIT-001 | BLOCKED | E | 门禁脚本/清单已就绪；沙箱外 ADB 实测 0 台已授权物理设备，待新 APK 后执行安装、10/60 分钟、触控、后台/锁屏、性能与日志验收 |
| SERVER-REALM-XP-001 | P1 | SERVER-HTTP-CONTRACT-001 | DONE | D/E | `6597769`：在线等级经验/累计修为分池、Battle/AFK/Quick AFK 双发放、旧 SQLite 一次迁移、突破消费与 profile/Unity 映射已通过；当前 HTTP 套件已扩展为 46/46 |
| SERVER-REALM-MATERIAL-002 | P1 | SERVER-REALM-XP-001 | DONE | D/E | `ca495ef`：独立破境石、RequiredLevel、按阶成本、大境界预留→Boss 原子结算、材料奖励、旧库升级、客户端防重与 46/46 HTTP 完成；残余失败冷却/退款政策转 Yellow/V0.2 |
| SETTINGS-UI-001 | P1 | SAVE-001 | DONE | C/D | 声音、震动、自动换装、保存、法律入口完成；真机触控待 QA-DEVICE |
| FUNNEL-001 | P1 | CORE-001 | DONE | D/E | 无 PII 本地 JSONL 漏斗完成；`bffd195` 增加首次大境界事件 |
| FEEDBACK-001 | P1 | CORE-001 | TESTING | C | 五类程序化音效与品质/战力反馈已编译；待场景听感和真机音量 |
| BALANCE-001 | P1 | CORE-STAGE-002 | TESTING | A/B/E | `91f17fb` 已删除 9999 HP、加入 24/8 Boss 与三败退守；真实恢复 9 场/6败/2刷关/≤120秒，10/60 smoke PASS；长期敌人成长和真机手感仍待验收 |
| AUTO-EQUIP-001 | P1 | CORE-001 | DONE | B/C | 默认开启、可关闭，只在统一战力严格提升时换装 |
| RC-QUALITY-001 | P0 | CORE-REALM-INTEGRATION-001,BUILD-ANDROID-003,QA-UI-PORTRAIT-001,QA-DEVICE-001 | TESTING | Direction/E | GATE 4 OPEN；不得用旧 109/26 或旧 APK/AAB放行 `ca495ef` |

当前自动扫描：Release-scope 无其他可执行 P0/P1；目标项目 Unity 会话可用后立即抢占 `QA-UNITY-POSTCHANGE-002`。
