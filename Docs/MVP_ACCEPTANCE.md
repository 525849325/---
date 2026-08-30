# Project ImmortalLoot MVP 验收矩阵

状态定义：`PASS` 表示当前工作区存在直接实现和自动化/运行证据；`PARTIAL` 表示领域或 API 已存在，但完整玩家交互或生产边界未闭环；`WAIVED` 表示用户明确批准不纳入本次 MVP 验收，但风险仍被记录。

## 最终玩家流程

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 登录并创建角色 | PASS | Main 明确提供离线演示与服务器登录两种入口；REST 网关和真实 Kestrel 建档/资料调用均验证通过，失败不会伪装成功。 |
| 自动战斗、击杀与技能 | PASS | Main PlayMode、纯 C# 战斗测试；主动/被动、CD、AOE、DOT、Buff/Debuff、Boss 狂暴均覆盖。 |
| 获得经验并升级 | PASS | Main 可见升级；服务端战斗权威发经验并按阈值升级。 |
| 随机装备与合法词条 | PASS | Main 可见词条；服务端生成唯一实例；真实配置 10,000 件统计通过。 |
| 穿戴并看到战力变化 | PASS | PlayMode 实际点击“穿戴最新装备”并断言统一 Power 上升；服务端穿戴同步 Power。 |
| 三类背包、筛选、比较、强化与分解 | PASS | 领域测试覆盖装备/材料/消耗品、排序筛选、批量分解和传奇锁定；服务器强化按共享公式幂等扣款并推进每日任务，挂机装备实际入包；原型页已接入比较、强化及安全分解。最新 PlayMode 2/2 通过，夜神竖屏实测可由底部导航进入装备页并穿戴新装备，战力从 986 更新为 1114。 |
| 1-1～1-10 与 Boss | PASS | 连续配置链；每关经验、灵砂、首通仙晶与 DropTable 均配置化，SQLite 验证十关权威通关、锁关拒绝、首通日志和 Boss 高品质下限。 |
| 离线挂机与快速挂机 | PASS | 客户端领域、服务器共享 `afk.json` 公式、基础 8 小时上限、卡权益额外上限、双倍活动和幂等均通过；在线活动页可领取普通及 2 小时快速挂机，服务器按 UTC 日限制免费与月卡次数。 |
| 境界突破与渡劫 | PASS | 客户端完整领域与服务器突破 API 通过；在线修炼/灵根页会提交幂等突破并展示权威结果。 |
| 随机灵根 | PASS | 大境界渡劫从未满级的九系配置中随机，落库 `PlayerSpiritualRoot`；重放总等级保持 1，资料接口和在线灵根页返回等级/上限。 |
| 学习功法并形成三套 Build | PASS | 火/雷/血主辅功法由配置学习和装备，`CultivationMethodStatProvider` 汇入统一属性/战力；最新 PlayMode 在真实 Main 场景连续切换雷修、血修、火修，并分别断言配置中的主辅功法名称。 |
| 排行榜 | PASS | 服务器永久/周榜、三维快照和防作弊通过；在线 UGUI 排行页会请求并展示权威战力榜及自身排名。 |
| 模拟仙晶购买 | PASS | 在线商城按钮已串联服务器建单→Mock Provider→回执验证→权益展示；服务器事务发放仙晶、首充法器/材料/挂机券，计算月卡/永久卡权益并提供每日仙晶幂等领取；Production 默认拒绝 Mock 回执，客户端不直接发币。 |
| 每日任务与活跃宝箱 | PASS | 六类任务和 20/40/60/80/100 宝箱由共享 `tasks.json` 驱动；SQLite 实际执行登录、推图、Boss、强化、5 次分解和成功渡劫，确认全部任务可领/已领；在线 UGUI 可逐项领取任务与活跃宝箱。 |
| 邮件奖励 | PASS | 过期过滤和附件幂等通过；在线 UGUI 会列出并领取首封未领取有效邮件。 |

## 架构、安全与数据

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| JSON 运行时配置 + ScriptableObject 编辑 | PASS | 统一 Repository、全表交叉验证、Authoring asset 与导出菜单。 |
| UI/表现与战斗数值分离 | PASS | AutoBattleEngine 为纯 C#；控制器只适配 UGUI。 |
| 服务器权威重要数值 | PASS | 客户端 REST DTO 不含 PlayerId/价格/奖励；服务器校验关卡解锁及战力门槛后才结算战斗，低战力 Boss 零奖励；货币、境界、排行和支付均由服务器计算，装备掉落读取共享关卡 DropTable、权重及品质边界。 |
| 支付/奖励/副本幂等 | PASS | SQLite 覆盖订单、交易号、首充、月卡/永久卡、每日权益、境界/终身购买限制、战斗、商城、任务、宝箱、邮件、挂机和分解重放；支付奖励同时写 Payment/Currency/Item/Equipment/Reward 日志。 |
| 六类日志 | PASS | Currency/Item/Equipment/Payment/Reward/Battle 表及写入验证。 |
| PostgreSQL/Redis 替换点 | PASS | 原目标允许开发使用 SQLite、只要求预留；持久化完全位于 EF Core DbContext，排行缓存通过 `IRankingCache` 隔离，可替换 Provider 而不改领域服务。 |
| 存档 | PASS | 服务端数据库存档；客户端非权威缓存使用版本、SHA-256、原子写入且不保存 Token。 |
| GM Debug | PASS | Development-only 页面按钮实际调用 `GameDebugService`，逐步覆盖资源/等级、指定词条 Mythic 装备、突破/关卡/灵根/功法、离线/充值及清档；PlayMode 全序列通过。 |
| 后续系统接口 | PASS | Pet/Guild/Trade/Auction/PvP/CrossServer/Season/Profession 只定义接口。 |

## 测试、性能与发布

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 指定系统 Unit Tests | PASS | Equipment、Affix、Damage、Power、Drop、AFK、Realm、Currency、Payment 全部存在。 |
| 10,000 件装备统计 | PASS | 真实 Mythic 与混合品质统计均通过。 |
| ≤20 单位 | PASS | 20 单位支持、21 单位拒绝；1,000×20 单位压力烟雾通过。 |
| 对象池与有界背包 | PASS | GameObjectPool 复用测试；2,000 掉落保持 120 格上限。 |
| 2 小时节奏 | PASS | 配置驱动真实 Player；虚拟单测与 120× soak 跑满 7,200 秒：24 采样、359 击杀、286/286 奖励窗口、0 待处理、背包 120、战力 1,181、内存 73.89–73.92 MB、零异常；PlayMode 单独覆盖玩家按钮闭环。真人记录保留为平衡调优而非 MVP 机械验收门槛。 |
| Unity EditMode / PlayMode | PASS | Unity 6000.5.10f1 于 2026-08-29 14:39 完成最新回归：EditMode 68/68、PlayMode 2/2，均为 0 failed、0 skipped；结果见 `TestResults/EditMode.xml` 与 `TestResults/PlayMode.xml`。PlayMode 已覆盖 Main 核心循环、全部必需原型页面、三套 Build、GM 全序列及服务器战斗/装备同步。 |
| Windows Player | PASS | 最新 Development Player 154,145,164 bytes；普通启动通过，无图形 soak 在全部奖励清空后于 61.80 秒自行退出。 |
| WebGL Player | PASS | Development 构建成功（18 文件、39,407,695 bytes / 98.96 秒），提供第二平台编译与裁剪证据，不冒充 Android 验证。 |
| Android APK 与模拟器 | PASS | Unity 6000.5.10f1 的 Android Build Support、SDK/NDK 与 OpenJDK 已安装；`Build/Android/ImmortalLoot-development.apk`（41,484,832 bytes）包含 ARMv7+ARM64。已在夜神 Android 7.1.2/x86+ARM 转译环境安装并启动，传统 `UnityPlayerActivity` 前台稳定；实测竖屏离线登录、自动战斗、连续击杀、Rare 随机词条掉落、六页导航及装备穿戴。2026-08-30 复验前台 PID 4281、PSS TOTAL 142,920 KB，日志无 FATAL/ANR。截图见 `Build/Android/immortalloot-nox-portrait-login.png`、`Build/Android/immortalloot-nox-loot.png`、`Build/Android/immortalloot-nox-equipment-nav.png` 与 `Build/Android/immortalloot-nox-equipment-action.png`。 |
| 低端 Android 物理真机 | WAIVED | 用户于 2026-08-30 明确批准本次 MVP 不做真机测试、继续以夜神完成 Android 验收。该项不冒充 PASS；`Tools/Android/Invoke-PhysicalDeviceAcceptance.ps1` 和 `Docs/PHYSICAL_ANDROID_TEST.md` 保留供未来正式发布认证使用。 |

## 当前结论

Phase 20 已在本次批准范围内完成：Windows/WebGL/Android 三平台构建、Unity EditMode 68/68、覆盖全部必需页面与三套 Build 的 PlayMode 2/2、后端权威验证、夜神实际运行和真实 Player 7,200 秒逻辑时轴均有直接证据。用户明确豁免物理真机测试；该豁免不等同于真机认证，未来面向商店发布前仍建议执行 `Docs/PHYSICAL_ANDROID_TEST.md`。逐项证据见 `Docs/COMPLETION_AUDIT.md`。
