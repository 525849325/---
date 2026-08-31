# 无人值守任务队列

状态只使用 `TODO / RUNNING / TESTING / BLOCKED / DONE / FAILED`。自动选择依赖已满足的最高优先级任务；外部阻塞不得停止其他 Workstream。

| ID | Priority | Dependencies | Status | Owner / Workstream | Result / 下一验收证据 |
|---|---|---|---|---|---|
| GITHUB-SYNC-001 | P0 | — | DONE | Direction/E | `origin/main` 与 GitHub `525849325/---` 已建立并持续 Push；远端主分支含 Unity 必需目录与 Docs |
| GITHUB-SYNC-002 | P1 | GITHUB-SYNC-001 | BLOCKED | Direction/E | 本地 `main=1370ee6`、本地跟踪 `origin/main=0ffbe55`；`848365d`、`2a40b29`、`1370ee6` 因 `github.com:443` 间歇 TCP 超时待 Push；非认证/权限问题，网络恢复后重试并远端验证 |
| AUDIT-001 | P0 | — | DONE | Direction | 项目审计、冲刺、Backlog、任务/RED/日报机制已建立 |
| CORE-001 | P0 | AUDIT-001 | DONE | A/B/C | 自动战斗、掉落、Boss、换装战力和核心循环基线完成 |
| CORE-STAGE-002 | P0 | CORE-001 | DONE | A/B/E | 胜利推进、失败重试、首 Boss 回环与奖励窗口离线/Unity/后端基线完成 |
| SAVE-001 | P0 | CORE-001 | DONE | D | v3 聚合存档、原子写入、迁移、损坏隔离与离线单次领取完成 |
| SAVE-HARDEN-001 | P0 | SAVE-001 | DONE | D/E | 唯一隔离名、I/O 安全降级与损坏存档延迟读取完成 |
| SAVE-AGGREGATE-001 | P0 | SAVE-001 | DONE | D/E | Stage/Realm/Cultivation/Root/Guide/Task 聚合恢复及累计修为迁移完成 |
| SAVE-WRITE-RESILIENCE-001 | P0 | SAVE-AGGREGATE-001 | DONE | D/E | `5a61ddf`：可恢复写入失败不再中断战斗，失败提示/重试恢复及 EditMode/PlayMode/Domain 回归完成 |
| COMM-001 | P0 | SAVE-001 | DONE | D/C | 商品结构、成长后曝光、Release 拒绝 Mock 与设置入口完成；真实渠道仍属外部发布项 |
| CORE-LOGIN-GATE-001 | P0 | SAVE-AGGREGATE-001 | DONE | D/E | 本地/服务器状态隔离和登录门禁完成 |
| SERVER-PROFILE-RECONCILE-001 | P1 | CORE-LOGIN-GATE-001 | DONE | D/E | 权威 profile/current-stage 镜像校正完成 |
| SERVER-BATTLE-AUTH-001 | P1 | SERVER-PROFILE-RECONCILE-001 | DONE | D/E | exact-current、单活动会话、并发幂等、失联恢复与旧 SQLite 升级通过 |
| SERVER-HTTP-CONTRACT-001 | P1 | SERVER-BATTLE-AUTH-001 | DONE | D/E | 真实 Kestrel HTTP 46/46 通过，含累计修为、破境石/pending 与拒绝零副作用 |
| SERVER-REALM-XP-001 | P1 | SERVER-HTTP-CONTRACT-001 | DONE | D/E | 在线双经验池、旧 SQLite 迁移、奖励、突破与 profile/Unity 映射已闭合 |
| SERVER-REALM-MATERIAL-002 | P1 | SERVER-REALM-XP-001 | DONE | D/E | 破境石、RequiredLevel、按阶成本、大境界 pending→Boss 原子结算、材料奖励与客户端防重已闭合 |
| CORE-REALM-INTEGRATION-001 | P0 | CORE-STAGE-002,SAVE-AGGREGATE-001 | TESTING | B/D/E | 离线/在线境界闭环已实现，静态编译、smoke、Backend、HTTP 46/46 PASS；待当前 Unity 125/34 |
| CORE-CYCLE-PACING-003 | P1 | CORE-STAGE-002,SAVE-WRITE-RESILIENCE-001 | TESTING | A/B/D/E | `848365d`：持久轮回、节奏门重置、敌人/奖励递增已实现；RealBattle 10 分钟第二 Boss 496.07/505.87 秒、2 胜无第三，60 分钟 11 胜6败 pending 0，120 分钟后继续；待 Unity/真机 |
| UI-PRODUCT-PAGES-002 | P1 | CORE-CYCLE-PACING-003 | TESTING | C/E | `1370ee6`：12/12 页真实只读摘要、占位扫描 0，离线导航不发奖励/存档/漏斗；在线假状态与 offline 商店文案经两路独立审查 APPROVE；待 Unity/视觉 |
| PRIVACY-CONSENT-001 | P1 | UI-PRODUCT-PAGES-002 | RUNNING | C/D/E | 增加明确接受/拒绝；分析默认关闭，Development 登录在同意前不发送数据，离线循环不受影响 |
| COMMERCIAL-INTENT-002 | P1 | UI-PRODUCT-PAGES-002,PRIVACY-CONSENT-001 | TODO | C/D/E | 增加 2–3 个可选商品意图和 shop_opened/product_selected/purchase_intent/result 漏斗；不收费、不伪造奖励，Release 继续拒绝 Mock |
| TASK-DAILY-001 | P1 | SAVE-AGGREGATE-001 | TESTING | B/C/D | 已诚实降级为“一次性成长试炼”，重载防重复测试源码通过静态编译且独立审查 APPROVE；待当前 Unity 运行证据 |
| PACKAGE-LOCK-CONSISTENCY-001 | P1 | — | TODO | E | 统一 manifest 与 packages-lock/runtime 版本并加入离线一致性检查 |
| ANDROID-PROVENANCE-001 | P1 | PACKAGE-LOCK-CONSISTENCY-001 | TODO | E | 构建前清除旧目标；生成 Git SHA sidecar；验证包名、版本、ABI、签名、Manifest/bundletool，真机门禁拒绝非同源 APK |
| QUALITY-GATE-002 | P1 | PACKAGE-LOCK-CONSISTENCY-001 | TODO | E | 外层强制执行静态四程序集、Domain/Core/Real/Balance、Backend/HTTP、安全/大文件与 Android 检查，产出绑定 SHA 的结果 JSON |
| REPO-SAFETY-001 | P1 | — | TODO | E | 将 staged/history secrets、签名材料、50/100 MiB、Unity 必需目录与 `.meta` 检查纳入本地门禁及 GitHub CI；当前扫描 0 命中、无需 LFS |
| SETTINGS-UI-001 | P1 | SAVE-001 | DONE | C/D | 声音、震动、自动换装、保存、法律入口完成；真机触控待 QA-DEVICE |
| FUNNEL-001 | P1 | CORE-001 | DONE | D/E | 无 PII 本地 JSONL 漏斗及首次大境界事件完成 |
| FEEDBACK-001 | P1 | CORE-001 | TESTING | C | 五类程序化音效与品质/战力反馈已编译；待场景听感和真机音量 |
| BALANCE-001 | P1 | CORE-CYCLE-PACING-003 | TESTING | A/B/E | 离线 RealBattle 10/60 分钟节奏、递增强度、失败恢复与 pending 归零 PASS；待当前 Unity/物理真机确认狂暴提示和手感 |
| AUTO-EQUIP-001 | P1 | CORE-001 | DONE | B/C | 默认开启、可关闭，只在统一战力严格提升时换装 |
| QA-UNITY-001 | P0 | CORE-STAGE-002,SAVE-AGGREGATE-001 | DONE | E | 历史检查点 `2bee3ac`：EditMode 109/109、PlayMode 26/26；不代表当前源码 |
| QA-UNITY-POSTCHANGE-002 | P0 | CORE-CYCLE-PACING-003,UI-PRODUCT-PAGES-002,PRIVACY-CONSENT-001,COMMERCIAL-INTENT-002,TASK-DAILY-001 | BLOCKED | E | 目标项目无可复用授权 Editor 会话；会话可用后跑当前预计 EditMode 125 / PlayMode 34、Console 与回归，不要求重复激活 |
| BUILD-ANDROID-002 | P0 | QA-UNITY-001 | DONE | E | 历史 `2bee3ac` APK/AAB 曾通过静态产物门禁；当前已 STALE |
| BUILD-ANDROID-003 | P0 | QA-UNITY-POSTCHANGE-002,ANDROID-PROVENANCE-001 | BLOCKED | E | 当前 Unity 全绿后重建同源 APK/AAB，并执行溯源、签名、ABI、Manifest 门禁 |
| QA-UI-PORTRAIT-001 | P0 | QA-UNITY-POSTCHANGE-002 | BLOCKED | C/E | 生成真正 1080×1920 十二页截图，检查字体、遮挡、技术文案与触控目标 |
| QA-DEVICE-001 | P0 | BUILD-ANDROID-003,QA-UI-PORTRAIT-001 | BLOCKED | E | 脚本/清单已就绪；当前 0 台已授权物理设备，待同源 APK 后执行安装、10/60 分钟、触控、后台/锁屏、性能与日志验收 |
| RC-QUALITY-001 | P0 | CORE-CYCLE-PACING-003,UI-PRODUCT-PAGES-002,PRIVACY-CONSENT-001,COMMERCIAL-INTENT-002,TASK-DAILY-001,QUALITY-GATE-002,BUILD-ANDROID-003,QA-UI-PORTRAIT-001,QA-DEVICE-001 | TESTING | Direction/E | GATE 4 OPEN；不得以旧 Unity 结果、旧 APK/AAB、模拟器或静态 smoke 放行当前源码 |

当前自动选择：`PRIVACY-CONSENT-001` 为依赖已满足的最高优先级可执行任务；`GITHUB-SYNC-002` 等待网络恢复但不阻塞其他 Workstream；Unity 目标会话出现后，P0 `QA-UNITY-POSTCHANGE-002` 立即抢占。
