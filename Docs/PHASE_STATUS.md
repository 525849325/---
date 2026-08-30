# Project ImmortalLoot 阶段状态

## Phase 1 — Unity 项目架构（完成）

### 完成文件

- `Assets/Game/Scenes/Main.unity`：可直接播放的原型场景。
- `Assets/Game/Editor/ProjectBootstrap.cs`：可重复生成场景并写入 Build Settings。
- `Assets/Game/Scripts/Battle/BattleSimulation.cs`：与动画/UI 解耦的纯 C# 自动战斗。
- `Assets/Game/Scripts/Equipment/*`：装备领域模型、加权随机词条与冲突处理。
- `Assets/Game/Scripts/UI/PrototypeGameController.cs`：场景表现适配层。
- `Assets/Game/Tests/EditMode/*`：战斗、装备、配置单元测试。
- `Assets/Game/Tests/PlayMode/*`：真实场景自动战斗与可见掉落测试。
- `Tools/Verification/*`：独立的 10,000 件装备统计验证。

### 核心架构

`BattleSimulation` 和 `EquipmentGenerator` 不持有 UI 或动画对象。`PrototypeGameController` 只推动模拟并刷新 UGUI。随机源通过 `IRandomSource` 注入，因此测试可复现。装备词条池与数量规则来自配置目录。

### 如何运行

使用 Unity 6000.5.10f1 打开项目，打开 `Assets/Game/Scenes/Main.unity` 后进入 Play Mode。场景会持续自动击杀“荒原妖兽”，并在掉落区展示“云纹青锋”的随机品质和随机词条。

### 如何测试

- Unity EditMode：3/3 通过。
- Unity PlayMode：1/1 通过，验证 Main 场景会在 5 秒内产生带词条的可见装备。
- 独立领域验证：生成 10,000 件装备并通过唯一 ID、合法词条、冲突、范围和品质统计检查。

### 当前问题

- Unity Personal、Editor/headless 与 Android entitlement 已确认有效；最新自动化回归仍需解决当前项目与 Hub/Editor LicensingClient 会话的绑定差异，不能再归因为用户未激活许可证。
- 原型表现仍是几何色块和 UGUI 文本，符合第一阶段占位要求。
- 战斗目前仅有普攻；技能、伤害公式和对象池属于后续 Phase 3–4。

## Phase 2 — 配置系统（完成）

### 完成文件与能力

- `IConfigSource` / `IConfigRepository` 分离读取位置与解析逻辑。
- `ResourcesConfigSource` 从运行时 JSON 加载配置。
- `JsonConfigRepository` 解析装备、词条与品质规则并建立只读 Catalog。
- 校验 schemaVersion、空表、重复 ID、数值范围、装备槽和跨表引用。
- `EquipmentGenerator` 的品质词条数量已经完全配置化。
- `GameplayConfigModels.cs`：Realm、Monster、Stage、DropTable、Skill、Cultivation、SpiritualRoot、Shop、Activity 全部强类型模型。
- `GameplayJsonConfigLoader.cs`：全表加载、枚举/时间解析、跨表引用与数值校验。
- `Assets/Game/Resources/Config/*.json`：12 份版本化运行时 JSON，包含原创种子内容。
- `JsonConfigAuthoringAsset.cs`：可在 Inspector 中编辑覆盖 JSON 的 ScriptableObject 桥接资产。
- `ConfigAuthoringTools.cs`：创建编辑资产、验证运行时 JSON、验证并导出覆盖配置。
- `Assets/Game/Data/GameConfigAuthoring.asset`：已绑定全部运行时配置的默认编辑资产。

### 如何运行与测试

- 菜单 `ImmortalLoot > Config > Validate Runtime JSON` 执行全表校验。
- 选择 `GameConfigAuthoring.asset` 后使用 `Validate and Export Selected Authoring Asset` 导出覆盖内容。
- Unity 自动化验证输出：1 equipment、10 realms、9 roots、3 skills、3 methods、2 monsters、2 stages、3 drop tables、2 shop items、1 activity。
- 配置加载、完整计数和故意损坏跨表引用测试均已通过。

### 当前问题

- 当前配置桥接以 JSON 文本覆盖为编辑单元；后续数据量增大时可增加表格型自定义 Inspector。
- 远端下载和签名校验属于后续“本地/服务器配置系统”集成阶段，当前通过 `IConfigSource` 保留替换点。

## Phase 3 — 角色属性（完成）

### 完成文件

- `Assets/Game/Scripts/Character/CharacterStats.cs`
- `Assets/Game/Scripts/Character/StatModifier.cs`
- `Assets/Game/Scripts/Character/CharacterStatAggregator.cs`
- `Assets/Game/Scripts/Character/CharacterStatService.cs`
- `Assets/Game/Tests/EditMode/CharacterStatAggregatorTests.cs`

### 核心架构

`CharacterStats` 覆盖生命、攻击、防御、会心、攻速、命中、闪避、吸血、增减伤、九系伤害和九系抗性。模块通过 `IStatModifierProvider` 提供来源明确的 Flat/加算百分比修改，不直接相互调用；聚合顺序固定为“基础值 + 固定值，再乘加算百分比”，并统一限制概率属性安全区间。

### 如何测试

Unity EditMode 共 7/7 通过，覆盖基础值不可变、固定值/百分比顺序、概率上限，以及装备/灵根等独立 Provider 的组合。

### 当前问题

- 装备、境界、灵根、功法的具体 Provider 将在其对应 Phase 接入，目前接口和聚合管线已经稳定。

### 下一阶段计划

进入 Phase 4：配置化伤害公式、技能 CD/自动释放、Buff/Debuff/DOT、暂停/加速/跳过表现和普通/精英/Boss 战斗状态。

## Phase 4 — 战斗系统（完成）

### 完成文件

- `Assets/Game/Scripts/Battle/DamageCalculator.cs`
- `Assets/Game/Scripts/Battle/DamageFormulaConfigLoader.cs`
- `Assets/Game/Scripts/Battle/AutoBattleEngine.cs`
- `Assets/Game/Resources/Config/battle_formula.json`
- `Assets/Game/Tests/EditMode/DamageCalculatorTests.cs`
- `Assets/Game/Tests/EditMode/AutoBattleEngineTests.cs`
- `Assets/Game/Scripts/UI/PrototypeGameController.cs`（迁移至新引擎）

### 核心架构

伤害结算依次应用技能倍率、防御曲线、元素增伤/抗性、暴击、通用增伤和减伤，最低伤害与上限来自 JSON。`AutoBattleEngine` 是纯 C# 逻辑层，支持主动/被动技能、自动 CD、单体/AOE、DOT、Buff、Debuff、治疗、吸血、Boss 狂暴、暂停、0.25–10 倍速和无表现快速结算。Encounter 支持 1–20 个敌人，逻辑事件与 UGUI 分离。

### 如何运行和测试

Main 场景会读取配置并用新引擎自动施放“烬痕诀”。Unity EditMode 16/16 通过，覆盖公式、暴击、最低伤害、DOT、Buff、暂停、倍速、跳过、Boss 狂暴、AOE 和 20 单位限制；PlayMode 1/1 通过，验证真实场景仍会自动击杀并显示掉落。

### 当前问题

- Buff/Debuff 当前只有通用增伤/减伤语义，后续技能扩展时增加可配置 StatModifier 列表。
- 当前表现层仍只显示主目标血条，逻辑层已经支持多敌人。

## Phase 5 — 怪物和关卡（完成）

### 完成文件

- `Assets/Game/Scripts/Stage/MonsterFactory.cs`
- `Assets/Game/Scripts/Stage/StageBattleFactory.cs`
- `Assets/Game/Scripts/Stage/StageProgressService.cs`
- `Assets/Game/Tests/EditMode/StageServicesTests.cs`

### 核心架构

`MonsterFactory` 只从 `MonsterConfig`、技能配置和章节缩放生成战斗 Actor；普通、精英、Boss 使用同一工厂。`StageBattleFactory` 将关卡怪物组装为 Encounter。`StageProgressService` 独立维护可序列化通关状态、进入条件、首通判定、普通奖励表和首通奖励表。每关在同源 `stages.json` 中配置推荐战力、经验、灵砂、首通仙晶、普通/首通 DropTable 与挂机倍率，后端直接消费这些字段。

### 如何测试

Unity EditMode 总计 18/18 通过。新增测试验证 1-1 解锁 1-10、首通奖励不可重复、Boss 配置生成、Boss 战斗可完成。

### 当前问题

- 第一章原创种子数据已扩充并验证为完整 1-1～1-10，其中 1-10 为 Boss 关。
- 服务当前是客户端领域实现；Phase 14 后端会复用规则并将通关结果改为服务器权威。

### 下一阶段计划

进入 Phase 6：扩展装备槽、基础属性、品质、特殊效果、套装字段、穿戴状态和装备实例持久化模型；随后 Phase 7 深化随机词条。

## Phase 6 — 装备系统（完成）

### 完成文件

- `Assets/Game/Scripts/Equipment/EquipmentModels.cs`
- `Assets/Game/Scripts/Equipment/EquipmentGenerator.cs`
- `Assets/Game/Scripts/Equipment/EquipmentLoadoutService.cs`
- `Assets/Game/Scripts/Equipment/EquipmentStatProvider.cs`
- `Assets/Game/Resources/Config/equipment.json`
- `Assets/Game/Tests/EditMode/EquipmentLoadoutTests.cs`

### 核心架构

装备实例持久化 `InstanceId/BaseId/Level/Quality/BaseStats/Affixes/SpecialEffectId/SetId/CreateTime/Source/IsLocked`。10 个装备槽全部独立，Ring1/Ring2 可同时穿戴。基础属性在生成时按等级快照，避免实例依赖表现资源。`EquipmentLoadoutService` 负责穿戴、替换、卸下和锁定；`EquipmentStatProvider` 将基础属性、词条、Mythic 特效和二/四件套统一转换为角色属性 Modifier；比较服务只替换候选装备所在槽位后计算关键属性差异。

### 如何运行和测试

运行时配置现包含 10 件原创基础装备、3 个 Mythic 特效和云迹二/四件套。Unity EditMode 21/21、PlayMode 1/1 通过，覆盖双戒指、替换、锁定、套装阈值、特效、比较和等级属性快照。

### 当前问题

- 装备强化与分解已在 Phase 9/14 后接入客户端领域和服务器权威接口；在线装备页可执行配置计费强化。
- 当前每槽仅有一件原创基础装备种子，Phase 20 数值/内容调优时扩展。

## Phase 7 — 随机词条（完成）

### 完成文件

- `Assets/Game/Scripts/Equipment/AffixGenerator.cs`
- `Assets/Game/Tests/EditMode/AffixGeneratorTests.cs`
- `Tools/Verification/DomainSmokeTests.cs`
- `Tools/Verification/run-domain-tests.ps1`

### 核心架构

独立 `AffixGenerator` 根据每件基础装备的合法池执行加权无放回抽取，同时约束唯一 AffixId 和 ConflictGroup。配置加载时预先计算每个池的最大合法容量，若无法满足最高品质 5 词条立即拒绝配置；运行时也不会静默少生成词条。Mythic 固定生成 5 词条并从配置池获得 1 个特殊效果。

### 如何测试

- Unity EditMode 总计 23/23 通过。
- 基于真实 10 类装备各生成 1,000 件 Mythic，共 10,000 件：ID 唯一、固定 5 词条、无非法词条、无冲突、数值均在区间内、特殊效果存在。
- 独立领域脚本再次生成 10,000 件混合品质装备并输出统计：Common 2000、Fine 3500、Rare 3000、Epic 1150、Legendary 300、Mythic 50，全部通过。
- PlayMode 1/1 通过，真实场景掉落仍正常。

### 当前问题

- 品质概率目前由掉落调用方决定；Phase 8 将把来源和品质范围统一并入 DropTable 服务。

### 下一阶段计划

进入 Phase 8：统一 DropTable 权重抽取、数量与品质范围、怪物/Boss/首通来源接入，并为基础保底/世界掉落保留策略接口。

## Phase 8 — 掉落系统（完成）

### 完成文件

- `Assets/Game/Scripts/Drop/DropTableService.cs`
- `Assets/Game/Resources/Config/drop_tables.json`
- `Assets/Game/Tests/EditMode/DropTableServiceTests.cs`
- `Assets/Game/Scripts/UI/PrototypeGameController.cs`（改为通过 DropTableService 掉落）

### 核心架构

怪物、精英、Boss、关卡、首通、挂机和活动共享 `DropContext`。服务按合法条件过滤词条，再进行配置权重抽取、数量区间和低品质偏重的品质区间抽取；装备条目直接生成完整 `EquipmentInstance`。`IDropPityPolicy` 可在不改服务的情况下插入保底，当前默认实现不启用保底，符合第一版基础权重要求。

### 如何测试

10,000 次普通关真实配置抽取落在 60/40 权重容差内；Boss 两次 Roll 和 Rare 品质下限、首通条件拒绝、外部保底策略均通过。Unity EditMode 27/27、PlayMode 1/1 通过。

### 当前问题

- 世界掉落和实际保底状态尚未启用，仅保留策略接口，符合第一版只实现基础权重随机的范围。
- 客户端与服务器均读取同源 `drop_tables.json`；服务器按关卡表、装备条目权重和品质上下限生成权威实例，普通关与 Boss 范围有 SQLite 直接断言。货币、材料等非装备奖励仍由各权威业务服务通过统一奖励事务发放。

## Phase 9 — 背包（完成）

### 完成文件

- `Assets/Game/Scripts/Inventory/InventoryModels.cs`
- `Assets/Game/Scripts/Inventory/InventoryService.cs`
- `Assets/Game/Scripts/Inventory/EquipmentDecompositionService.cs`
- `Assets/Game/Resources/Config/inventory_formula.json`
- `Assets/Game/Tests/EditMode/InventoryServiceTests.cs`

### 核心架构

装备使用独立实例列表，材料和消耗品按 ItemId 堆叠。背包服务负责容量、重复实例防护、槽位/品质筛选和品质/等级/时间/槽位排序，查询不会改变存档顺序。分解收益按等级、品质和 JSON 倍率计算；Legendary/Mythic 获得时默认锁定，单件分解拒绝锁定装备，一键分解即使传入更高阈值也硬性保护 Legendary 以上。

### 如何测试

Unity EditMode 31/31 通过，覆盖容量、重复 ID、堆叠、筛选排序、传奇默认锁定、锁定拒绝和批量分解保护/收益。

### 当前状态

- 离线原型背包页现已调用真实排序/筛选和配置驱动分解公式，批量发放灵砂、强化石与装备精华，并保护 Legendary/Mythic、锁定及已穿戴装备。
- 装备页会在穿戴前展示攻击、生命、防御差异；材料与消耗品分类计数会在背包页显示。
- 在线模式的单件分解仍由服务器 `/equipment/decompose` 保持货币、材料及六类日志的事务权威；批量服务器端点不是第一阶段必需接口。
- 页面接线后的 PlayMode 已重新执行并以 2/2 通过；测试曾发现装备页缺少玩家可点击入口，补齐底部“装备”导航并重建 Main 场景后恢复通过。

### 下一阶段计划

进入 Phase 10：离线时长、8 小时上限、经验/金币/材料/装备掉落次数、活动和玩家加成，以及每日免费快速挂机接口。

## Phase 10 — 挂机系统（完成）

### 完成文件

- `Assets/Game/Scripts/AFK/AfkRewardService.cs`
- `Assets/Game/Resources/Config/afk.json`
- `Assets/Game/Tests/EditMode/AfkRewardServiceTests.cs`

### 核心架构

`AfkRewardCalculator` 是纯计算模块，输入离线秒数、关卡效率、玩家加成、活动加成和额外离线上限，输出有效时长、经验、金币、材料和装备抽取次数。默认上限 8 小时且全部基准值来自 JSON。客户端领域与服务器 `AfkAuthorityService` 都只读取权威时钟；服务器会叠加有效月卡/永久卡离线上限。普通领取推进离线时间且幂等；`POST /afk/quick-claim` 按配置生成 2 小时收益，以 UTC 日限制基础免费 1 次及月卡加成次数。

### 如何测试

Unity EditMode 总计 35/35 通过。新增测试覆盖 10 小时离线截断为 8 小时、五类收益、关卡/玩家/活动乘区、额外离线上限、重复领取为零，以及快速挂机 UTC 日切重置。

### 当前问题

- `IServerClock` 当前由调用方注入；Phase 14 后端实现会提供真实服务器时间。
- 普通与快速挂机的装备抽取次数现会在同一服务器事务中生成唯一装备实例、合法词条和 EquipmentLog；重放不会重复入包。
- 月卡/永久卡额外离线上限及快速挂机次数已接入服务器权威商业权益计算；在线活动页会实际领取普通与快速挂机收益。

### 下一阶段计划

进入 Phase 11：境界 1～10 阶、经验/等级/资源条件、突破成功率、失败冷却或材料损失、属性与系统解锁结果。

## Phase 11 — 境界系统（完成）

### 完成文件

- `Assets/Game/Scripts/Realm/RealmProgressionService.cs`
- `Assets/Game/Resources/Config/realm_formula.json`
- `Assets/Game/Tests/EditMode/RealmProgressionServiceTests.cs`
- `Assets/Game/Scripts/Core/IServerClock.cs`

### 核心架构

每个配置境界支持 1～10 阶。小阶突破校验等级、经验、材料和服务器冷却，成本随当前阶数增长；成功扣经验/材料并只升一阶，失败不掉境界、不扣经验，只损失配置比例材料并进入冷却。第 10 阶会预留目标境界材料并生成唯一渡劫 Token，成功战斗回调后才切换境界；错误 Token、重复开始和重复结算均被拒绝。成功结果返回新系统和每日副本增量。`RealmStatProvider` 累计已完成境界与当前阶属性。服务器通过 `IServerRandomSource` 隔离概率和灵根抽取：生产使用加密随机，集成测试注入固定源以消除概率性失败。

### 如何测试

首次测试发现 float `Ceiling` 边界多扣 1，已改为整数基数最后除法。修复后 Unity EditMode 41/41 通过，覆盖小阶成功/失败、冷却、渡劫挂起、Token 幂等、失败返还、系统解锁、属性累计和最高境界。

### 当前问题

- 渡劫使用现有战斗引擎回调，但尚未制作专用表现场景。
- 材料扣减将在后端阶段接入事务日志。

## Phase 12 — 灵根（完成）

### 完成文件

- `Assets/Game/Scripts/SpiritualRoot/SpiritualRootService.cs`
- `Assets/Game/Tests/EditMode/SpiritualRootServiceTests.cs`

### 核心架构

九系灵根进度全部持久化。渡劫成功使用 Token 随机选择一个未满级灵根并 +1，同一 Token 记录发放结果后不可重复；满级灵根不会进入候选池，全部满级时安全返回。伤害、抗性通过 `SpiritualRootStatProvider` 接入统一属性管线，元素技能加成可单独查询。重置和洗练保留独立接口但首版不实现。`TribulationRewardCoordinator` 只在境界真实晋升后发放灵根。

### 如何测试

Unity EditMode 45/45 通过，覆盖随机候选、Token 幂等、满级、伤害/抗性/技能加成，以及渡劫成功一次性集成。

### 当前问题

- 灵根重置/洗练仅有接口，符合第一版范围。

## Phase 13 — 功法（完成）

### 完成文件

- `Assets/Game/Scripts/Cultivation/CultivationMethodService.cs`
- `Assets/Game/Resources/Config/cultivation_methods.json`
- `Assets/Game/Tests/EditMode/CultivationMethodServiceTests.cs`

### 核心架构

功法具有 Normal/Rare/Epic/Legendary/Ancient 品质、解锁境界、主修/辅助类型和多维加成。服务支持幂等学习、1 个主修槽和 2 个辅助槽，拒绝未学习、错误槽型和重复辅助装配。属性 Provider 处理攻击、生命、吸血、暴击和九系伤害；服务另提供技能倍率、挂机、Boss 和低生命增伤查询。

### 三套 Build

- 火修：`烬阳归藏篇 + 流烬息法`，攻击 +7%、火伤 +17%、火技能倍率 +7%、Boss 伤害 +3%。
- 雷修：`九霄鸣脉录 + 惊霆引`，攻击 +3%、暴击 +6%、雷伤 +14%。
- 血修：`玄血生息章 + 归血小周天`，生命 +23%、吸血 +7%、低血增伤 +28%、挂机 +8%。

### 如何测试

Unity EditMode 50/50、PlayMode 1/1 通过，覆盖境界锁、学习幂等、主辅槽规则和三套 Build 的明确数值差异。

### 当前问题

- 低血增伤和 Boss 加成已由服务提供，Phase 20 完整 Build 装配时注入战斗上下文。

### 下一阶段计划

进入 Phase 14：ASP.NET Core 后端、EF Core 数据模型、SQLite 开发存储、权威战斗/奖励边界和 REST API 骨架。

## Phase 14 — ASP.NET Core 后端（完成）

### 完成文件

- `Backend/src/ImmortalLoot.Server/ImmortalLoot.Server.csproj`
- `Backend/src/ImmortalLoot.Server/Persistence/GameEntities.cs`
- `Backend/src/ImmortalLoot.Server/Persistence/GameDbContext.cs`
- `Backend/src/ImmortalLoot.Server/Services/BattleAuthorityService.cs`
- `Backend/src/ImmortalLoot.Server/Services/ServerClock.cs`
- `Backend/tests/ImmortalLoot.Server.Verification/*`

### 核心架构

后端采用 ASP.NET Core 8、EF Core 与 SQLite 开发存储。数据模型覆盖账号、玩家、属性、背包、装备、技能、功法、灵根、关卡、货币、邮件、任务、购买、支付、排行榜快照，以及货币/道具/装备/支付/奖励/战斗日志。战斗开始与结算均由服务端创建和读取会话，经验、灵砂、首通仙晶及装备表均从共享关卡配置计算，不接受客户端输入；奖励、余额和日志在同一数据库事务内落库，重复结算通过幂等键返回原结果且不重复发奖。统一货币服务复用 EF 本地跟踪钱包，支持首次建档同事务发放多币种。

### 如何运行和测试

Release 构建 0 warning、0 error。独立 SQLite 验证程序通过建库、战斗开始、首次结算、重复结算、余额和日志一致性检查。真实 Kestrel HTTP 链路也已验证：登录、读取角色、开始战斗、首次结算和重复结算后，最终软货币只增加一次 10。

### 当前问题

- 开发环境当前使用 `EnsureCreated`；部署 PostgreSQL 前需要建立正式 EF Core migration 流程。
- 当前权威战斗由服务器校验关卡解锁及服务器保存的等级/战力门槛，再计算经验/货币、首通与配置化装备；低战力 Boss 会话无法结算且不会写奖励或背包。尚未在服务端逐帧复算 Unity 战斗过程，属于后续反作弊强化。
- Redis 留到排行榜和热点数据阶段接入。

## Phase 15 — 登录与角色接口（完成）

### 完成文件

- `Backend/src/ImmortalLoot.Server/Services/AuthService.cs`
- `Backend/src/ImmortalLoot.Server/Services/PlayerQueryService.cs`
- `Backend/src/ImmortalLoot.Server/Program.cs`

### 核心架构

游客首次登录在单个事务中创建账号、角色、初始属性和货币存档；同一游客标识再次登录复用原角色。登录 Token 使用 32 字节安全随机数生成，数据库只保存 SHA-256 摘要和有效期。角色资料、背包、战斗开始与结算均从 Bearer Token 解析当前玩家，不接受客户端指定 PlayerId。

### 首版接口

- `POST /auth/login`
- `GET /player/profile`
- `GET /player/inventory`
- `POST /battle/start`
- `POST /battle/finish`
- `GET /health`

### 如何测试

SQLite 验证覆盖首次游客建档、重复登录得到同一角色、有效/无效 Token、默认角色资料，以及战斗奖励幂等。真实 HTTP 烟雾测试返回首次 `Replayed=false`、第二次 `Replayed=true`，最终余额与单次奖励完全一致。

### 当前问题

- 首版仅实现游客登录；账号绑定、设备迁移和 Token 刷新/吊销管理待后续扩展。
- 极端并发的同一游客首次注册需要增加唯一键冲突后的查询重试。

### 下一阶段计划

进入 Phase 16：统一 CurrencyService、商品配置、购买校验、每日/终身限购，以及商城页面领域接口。

## Phase 16 — 商城与统一货币服务（完成）

### 完成文件

- `Assets/Game/Scripts/Shop/CurrencyService.cs`
- `Assets/Game/Scripts/Shop/ShopService.cs`
- `Assets/Game/Tests/EditMode/ShopServiceTests.cs`
- `Backend/src/ImmortalLoot.Server/Services/CurrencyService.cs`
- `Backend/src/ImmortalLoot.Server/Services/ShopService.cs`
- `Backend/src/ImmortalLoot.Server/Persistence/GameEntities.cs`（购买计数与幂等收据）

### 核心架构

客户端 `CurrencyService` 统一余额、版本、事务记录和幂等键；`ShopService` 读取现有 `ShopItemConfig`，校验解锁条件、每日/每周/终身限购、数量和余额后发放背包道具。服务端是最终权威：`POST /shop/purchase` 不接受客户端价格或货币类型，在单个数据库事务中执行幂等检查、扣款、限购计数、入包、货币日志和道具日志。战斗奖励也迁移到同一个服务端 `CurrencyService`，避免模块直接写余额。

### 如何测试

- Unity EditMode 53/53、PlayMode 1/1 通过。
- ASP.NET Core Release 构建 0 warning、0 error。
- SQLite 集成验证覆盖购买幂等、精确扣款、一次发货、每日限购和境界锁；同时回归登录与战斗奖励幂等。

### 当前问题

- 服务端商城与六类商业商品现直接加载 Unity Resources 同源 JSON；构建会复制完整配置目录，后端验证覆盖目录数量、引用和规则，避免人工同步漂移。
- 当前只提供商城领域接口和 API，完整 UGUI 商品列表与购买反馈会在 Phase 20 客户端整合时制作。
- 本地 SQLite 开发数据库因可能含可恢复测试数据而保留，未执行破坏性清理。

### 下一阶段计划

进入 Phase 17：战力/境界/关卡排行榜、周期快照、分页查询和 Redis 缓存替换点。

## Phase 17 — 排行榜（完成）

### 完成文件

- `Backend/src/ImmortalLoot.Server/Services/RankingService.cs`
- `Backend/src/ImmortalLoot.Server/Persistence/GameDbContext.cs`（周期唯一与按名次查询索引）
- `Backend/src/ImmortalLoot.Server/Program.cs`（排行榜查询接口）

### 核心架构

Power、Realm、Stage 三类榜单全部从服务器数据库重建，客户端没有提交 Score 的接口。境界榜按十境顺序和 1～10 阶编码，关卡榜只统计已通关记录。快照以 PlayerId + RankingType + PeriodKey 唯一，按 Score 降序、PlayerId 升序形成稳定名次；分页支持 1～100 条，并在有效 Bearer Token 存在时返回自身排名。`IRankingCache` 隔离缓存层，当前线程安全内存实现可替换为 Redis，快照刷新会清除旧缓存。

### 如何测试

Release 构建 0 warning、0 error。SQLite 集成验证覆盖两名玩家的三榜共六条快照、战力分页、自身名次、境界 304 分与关卡 10010 分计算。

### 当前问题

- 当前周期为 UTC 日榜，赛季/周榜规则和历史归档待活动配置驱动。
- `MemoryRankingCache` 只适合单实例开发环境；多实例部署必须实现 Redis 版本及分布式刷新锁。
- 快照目前查询缺失时同步生成；生产环境应由后台调度任务定期刷新。

### 下一阶段计划

进入 Phase 18：支付 Provider、Mock 支付、服务端订单验证与防重复发货。

## Phase 18 — 支付 Mock 与安全边界（完成）

### 完成文件

- `Assets/Game/Scripts/Payment/PaymentProvider.cs`
- `Assets/Game/Scripts/Payment/CommercialEntitlementService.cs`
- `Assets/Game/Resources/Config/commercial_products.json`
- `Backend/src/ImmortalLoot.Server/Services/PaymentService.cs`
- `Backend/src/ImmortalLoot.Server/Persistence/GameEntities.cs`

### 核心架构

客户端 `IPaymentProvider` 只获取平台回执，`MockPaymentProvider` 用于开发；任何 Provider 都不能直接发货。服务端订单保存权威金额、币种和商品，`IPaymentReceiptVerifier` 验证回执，平台交易号唯一，同一订单重复验证不会重复发放。未配置真实平台时默认拒绝。数据配置包含初缘首充、月卡、永久卡、每日礼包、境界礼包与仙晶直购六类；服务端事务校验境界与终身限购，首笔验证付款一次性发放原创传奇法器、材料和快速挂机券。`GET /payment/entitlements` 权威计算期限/永久权益，`POST /payment/daily-claim` 按 UTC 日幂等发放每日仙晶。

### 如何测试

Unity 领域测试覆盖 Mock 回执、六类商品、首充一次性、月卡到期、永久卡、境界锁、终身限购及客户端权益 API，并已纳入最新 EditMode 68/68 回归。SQLite 集成验证覆盖两个待支付订单、首充服务器发货、每日礼包道具、月卡/永久卡叠加、每日领取重放、境界锁、终身限购、统一 RewardLog、重复验证和平台交易号复用拒绝；Release 为 0 error，仅因受限网络产生 1 个 NU1900 漏洞源警告。

### 当前问题

- 上线前必须实现目标商店 verifier、密钥管理、退款回调与网络重试。
- 真实平台 SDK 占位 Provider 会明确抛出未配置错误，不会误发货。

### 下一阶段计划

进入 Phase 19：六类每日任务、活跃宝箱、活动时间窗、邮件附件和奖励幂等。

## Phase 19 — 每日任务和活动（完成）

### 完成文件

- `Backend/src/ImmortalLoot.Server/Services/TaskService.cs`
- `Backend/src/ImmortalLoot.Server/Services/ActivityService.cs`
- `Backend/src/ImmortalLoot.Server/Services/MailService.cs`
- `Backend/src/ImmortalLoot.Server/Services/RewardService.cs`

### 核心架构

每日任务覆盖登录、推图、Boss、强化、分解和渡劫，完成后提供活跃度；20/40/60/80/100 五档宝箱分别使用唯一奖励键。客户端不能上报任务进度，登录、战斗、强化、分解和大境界渡劫等服务器权威事件负责推进。活动按服务器 UTC 时间窗过滤，首个活动为双倍挂机框架。邮件支持已读、附件、过期和领取状态，任务、宝箱与邮件全部通过统一奖励事务防重放。

### 如何测试

SQLite 验证六类目录及六种真实行为，包含三次推关、Boss、一次强化、五次分解和确定性成功的大境界渡劫，并确认全部任务已领取或可领取；另覆盖任务/宝箱重放、过期邮件、附件重放和活动时间窗。同一 Release 验证程序集连续运行三次均通过；受限网络下仅有 NU1900 漏洞源警告。

### 当前问题

- 服务器强化读取共享背包公式、幂等扣款并推进 `EquipmentEnhance`；分解推进 `EquipmentDecompose`，渡劫推进 `Tribulation`。在线装备页已接强化，背包/境界页分别接分解与突破。
- 活动目录需与 Unity JSON 纳入同源签名发布流程。

### 下一阶段计划

进入 Phase 20：整体测试、完整可玩闭环、UGUI、性能、2 小时节奏和最终验收。

## Phase 20 — 整体测试（历史快照；当前 RC Gate 已重新打开）

> 本节记录 2026-08-29 原型验收历史。它已被当前 `bffd195` 的 RC Gate 取代：当前 Unity 111/28 未运行，历史 Android 产物已过期，旧真机豁免不再作为当前放行条件。

### 已完成

- Main 场景登录、自动战斗、经验/金币、十关推进、随机装备、点击穿戴和统一战力增长闭环。
- 统一 UGUI 页面壳、弱引导、GM 命令、对象池和未来服务接口。
- 服务器权威关卡解锁、通关、装备词条掉落、挂机、境界、商业化与奖励幂等。
- 客户端统一 REST 网关只提交意图和幂等键；Bearer Token 不写入本地存档。
- 本地缓存存档具有 schemaVersion、SHA-256 校验和、篡改拒绝和原子替换。
- 永久榜与周榜独立快照；双倍挂机活动已实际乘入服务器收益。
- Unity 6000.5.10f1 于 2026-08-29 14:39 完成当时原型回归：EditMode 68/68、PlayMode 2/2，均为 0 failed、0 skipped；该历史结果不代表当前 `bffd195`。后端 Release 与 SQLite 权威集成验证当时通过。
- 压力烟雾覆盖 1,000 场二十单位战斗与 2,000 次掉落/120 格背包轮转。
- Windows Development Player 构建成功（约 154 MB），隐藏启动 6 秒无异常或崩溃。
- 真实 Kestrel HTTP 烟雾覆盖建档、资料、战斗/重放、装备入包、商城、周榜、六类任务和活动；服务停止后端口无残留监听。

### 历史发布记录与当前边界

- 逐项验收矩阵见 `Docs/MVP_ACCEPTANCE.md`；UGUI 页面现均有可点击操作与反馈。
- Android Build Support 与依赖模块已安装，历史 APK 曾在夜神完成安装和核心循环验证。早期“不做物理真机”的范围豁免不适用于当前 RC Gate；模拟器结果不构成当前源码或物理真机放行。
- `PlayerBuildTools.BuildAndroidDevelopmentApk` 会输出 `Build/Android/ImmortalLoot-development.apk`，并固定包名、双 ARM 架构、旧系统 Activity 入口与必需 UGUI 着色器。
- Development Player 已加入非 batch 的五分钟遥测采样器；`Docs/TWO_HOUR_PLAYTEST.md` 定义 0–120 分钟检查点、证据路径和不得预填的人工结论。
- `DemoPacingSession` 已把 `demo_pacing.json` 接入真实 Player：发布默认 1×实时推进，测试才可显式加速；确定性虚拟 120 分钟验证掉落次数、成长脉冲、1-10 Boss 与所有里程碑。最新 Player 已验证遥测文件实际写盘。
- Development Player 支持显式 QA 参数 `-playtestSpeed`/`-playtestAutoQuit`；真实可执行文件的 120× soak 完成 7,200 秒逻辑时轴、359 击杀、286/286 奖励窗口、0 待处理、120 有界背包与 24 个采样，零异常，详见 `Docs/Evidence/ACCELERATED_SOAK_2026-08-29.md`。
- 服务端权威目录已迁移为 Unity JSON 同源加载：商城、商业商品、活动、装备/词缀、品质、境界、关卡、灵根、任务与 AFK 均不再维护重复常量；Release 构建 0 warning/0 error，权威领域验证通过。
- UGUI 已按登录模式分流：离线按钮保留完整演示，服务器模式下角色、背包/装备、商城、境界/灵根、排行榜、邮件、任务和挂机直接调用权威 REST；Kestrel 冒烟验证了鉴权与所有依赖响应形状。
- 在线自动战斗现以本地表现驱动服务器 `start/finish` 权威结算，随后读取装备实例 JSON 展示随机词条；“穿戴最新装备”提交服务器并刷新战力。真实 HTTP 闭环验证 Rare 掉落、3 条词缀、穿戴与战力更新。
- Mock 支付仅在 `Development` 且显式配置开启时注册；服务器校验订单号、商品、金额、币种和交易号。真实 HTTP 验证 Development 发放 60 仙晶，Production 对同回执返回 409。
- 服务器模式 PlayMode 回归覆盖权威战斗、随机词条背包同步与穿戴；测试同时发现并修复 Unity 登录 DTO 与 ASP.NET camelCase 响应不匹配的问题，避免真实登录被旧假响应掩盖。
- 灵根权威链经复核与加强：渡劫只抽取未满级配置、持久化九系等级、资料接口返回等级/上限，奖励重放不重复增长。
- 修炼页现使用真实 `CultivationMethodService` 学习/装备三套主辅功法，并由统一属性服务重算战力；GM Debug 页连接全命令服务，PlayMode 覆盖完整命令序列。
- 最新 Windows Development Player 重建成功（154,138,538 bytes / 4.08 秒），隐藏启动 6 秒无错误；WebGL Development Player 构建成功（39,407,695 bytes / 98.96 秒）。
- Unity Android Build Support、SDK/NDK 与 OpenJDK 已安装；构建脚本固定包名、ARMv7+ARM64、传统 Activity 入口，并强制包含 UGUI/Sprite 默认着色器。
- Android Development APK 已生成于 `Build/Android/ImmortalLoot-development.apk`（41,484,832 bytes），在夜神 Android 7.1.2 上安装并稳定前台运行；竖屏离线登录、自动战斗、19 次击杀、Rare 两词条掉落、六页导航和装备穿戴实测可见，战力从 986 更新为 1114，PSS TOTAL 约 139 MB，无 FATAL/ANR。
- 夜神仅保留为历史 Android 功能与兼容参考；物理真机的温升、长时内存、休眠恢复、触控和帧率是当前 GATE 4 必需证据。
- `Tools/Android/Invoke-PhysicalDeviceAcceptance.ps1` 已把该门槛固化为标准 120 分钟采集流程：安装/冷启动、逐分钟 PSS/电量/温度/前台状态、thermal、gfx framestats、logcat 与人工触控/休眠/弱网签署表；设备检查已实测拒绝夜神 `127.0.0.1:62001`，不会生成伪真机证据。
- 以上自动化、三平台构建与模拟器闭环仅描述当时原型。当前 Phase 20 / GATE 4 未完成，必须以 `DAILY_REPORT.md`、`TASK_QUEUE.md` 和 `MVP_ACCEPTANCE.md` 的当前证据为准。
