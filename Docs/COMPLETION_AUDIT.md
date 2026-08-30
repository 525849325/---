# Project ImmortalLoot 完成度审计

审计基准：`goal-objective.md` 的 Phase 1–20、最终玩家流程、三套 Build、服务器权威、幂等/日志、测试、性能与发布要求。证据只接受当前工作区源码、机器生成的测试结果、构建产物及实际运行截图；设计意图不作为完成证据。

## Phase 与核心系统

| 范围 | 结论 | 权威证据 |
|---|---|---|
| Phase 1–5：项目、配置、属性、战斗、关卡 | 已证明 | `Assets/Game/Scripts` 的分层领域代码、运行时 JSON、Main 场景；`TestResults/EditMode.xml` 68/68 与 PlayMode 自动战斗/推进断言。 |
| Phase 6–10：装备、词条、掉落、背包、挂机 | 已证明 | 通用装备/词条/掉落服务、10,000 件统计、三类背包与安全分解、AFK 权威服务；PlayMode 实际掉落、比较、穿戴、筛选与分解。 |
| Phase 11–13：境界、灵根、功法 | 已证明 | 配置驱动突破/渡劫/九系灵根；场景级 PlayMode 连续切换雷修、血修、火修并断言各自主辅功法。 |
| Phase 14–19：后端、账号存档、商店、排行、支付、任务活动 | 已证明 | ASP.NET Core 8 + EF Core SQLite；`Backend/tests/ImmortalLoot.Server.Verification` 覆盖权威领域、交易与奖励重放；全部目标 REST 路由存在。 |
| Phase 20：整体自动化与三平台 | 已证明（批准范围） | Unity EditMode 68/68、PlayMode 2/2；后端权威验证；Windows/WebGL/Android 构建；夜神核心循环；7,200 秒逻辑时轴均已证明。用户明确批准本次 MVP 豁免物理真机测试。 |

## 最终可玩流程

`Assets/Game/Tests/PlayMode/PrototypeSceneTests.cs` 当前直接驱动 Main 场景，验证登录入口、自动击杀、经验/推关、随机词条装备、穿戴与战力变化、角色、装备、三类背包、修炼、灵根、关卡、商店、排行榜、邮件、任务、活动和 GM Debug。服务器模式另验证 `/battle/start`、`/battle/finish`、`/player/inventory`、`/equipment/equip` 与资料战力同步。

服务器权威的真实数据库流程由 `Backend/tests/ImmortalLoot.Server.Verification/Program.cs` 验证：游客建档、十关/Boss、装备生成与穿戴、挂机、境界/灵根、商城、六类任务、活动、邮件、排行榜、支付和重复请求均使用 SQLite 状态与日志断言，不以客户端假数据决定奖励。

## 数据、安全与工程边界

- 装备、词条、品质、怪物、关卡、掉落、技能、境界、灵根、功法、挂机、商店、商业商品、任务和活动均以 `Assets/Game/Resources/Config/*.json` 为运行时数据源。
- Currency、Item、Equipment、Payment、Reward、Battle 六类日志已建模并由后端验证程序检查关键写入。
- 战斗/支付/购买/挂机/任务宝箱/邮件/分解/境界等奖励边界具有服务器幂等键、数据库唯一索引或重放断言。
- PostgreSQL 替换点位于 EF Core Provider；Redis 替换点位于 `IRankingCache`。MVP 开发存储按目标许可使用 SQLite。
- 未来 Pet/Guild/Trade/Auction/PvP/CrossServer/Season/Profession 只保留接口，没有扩张为第一版复杂 MMO。

## 测试与构建证据

| 证据 | 当前结果 |
|---|---|
| `TestResults/EditMode.xml` | 68/68 passed，0 failed，0 skipped；2026-08-29 14:39。 |
| `TestResults/PlayMode.xml` | 2/2 passed，0 failed，0 skipped；2026-08-29 14:38；包含全部必需页面与三套 Build 场景级断言。 |
| 后端验证程序 | 权威 API 领域、商业化、排行榜、Live Ops、AFK、装备、境界幂等全部通过。 |
| Windows Player | Development Player 构建与无图形自动退出 soak 通过。 |
| WebGL Player | Development 构建通过。 |
| Android APK | ARMv7+ARM64 APK 已在夜神 Android 7.1.2 安装运行；竖屏登录、战斗、掉落、导航及穿戴可见。 |
| 2 小时节奏 | 120× Player soak 跑满 7,200 秒逻辑时轴；真人记录保留为后续平衡调优。 |

## 范围豁免与残余风险

用户于 2026-08-30 明确要求继续使用夜神并不做真机测试。因此低端物理 Android 手机的安装、120 分钟运行、真实帧率、内存、温升、电量、触控、后台/锁屏恢复和弱网测试不属于本次 MVP 完成条件。`Tools/Android/Invoke-PhysicalDeviceAcceptance.ps1` 与 `Docs/PHYSICAL_ANDROID_TEST.md` 继续保留，供未来面向商店发布时补做；夜神证据不被表述为物理真机证据。

在上述用户批准的范围变更下，Phase 1–20 的全部必需实现、自动化、三平台构建、2 小时逻辑体验和夜神 Android 功能验收均有直接证据，可将本次 MVP 目标标记为完成。
