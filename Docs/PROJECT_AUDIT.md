# 《太初：无尽轮回》V0.1 项目审计

> 当前状态补充（2026-08-30）：本文主体是启动审计快照。最新权威状态见 `DAILY_REPORT.md` 与 `TASK_QUEUE.md`。当前源码为 `bffd195`：离线境界/渡劫闭环已接入，程序集编译及离线 smoke PASS；当前 Unity EditMode 111 与 PlayMode 28 尚未运行。`2bee3ac` 的 109/109、26/26 和 APK/AAB 仅为历史证据且产物已过期。GATE 4 仍缺当前 Unity 回归、重建双产物、完整竖屏视觉与物理真机验收。

审计日期：2026-08-30  
审计基线：安全检查点 `ced7a07` 之后的当前工作区。状态只依据当前源码与本轮重新运行的测试/构建结果；旧报告中的历史 PASS 不自动继承。

## 结论

选择“复用领域层，重构产品层”，不建立全新 Unity 工程。现有纯 C# 战斗、装备、掉落、属性、关卡、境界、挂机、商业化接口和后端权威逻辑有较高复用价值；主要风险集中在主场景集成、真实存档/离线闭环、前 10 分钟节奏、视觉反馈、设置、Release 构建和当前证据刷新。重写整个项目预计比修通这些产品层缺口更慢且风险更高。

## 1. 当前工程状态

- Unity：6000.5.10f1。
- Render Pipeline：Built-in（`m_CustomRenderPipeline: 0`），Gamma 色彩空间。
- 目标：Android 优先；当前配置允许竖屏自动旋转，Android ARMv7 + ARM64，最低 API 26；产品名仍为《太初拾遗录》，包名 `com.immortalloot.prototype`。
- 依赖：UGUI 2.0.0、Unity Test Framework 1.4.6、UnityWebRequest；后端为 ASP.NET Core 8 + EF Core SQLite 8.0.21。
- Build Settings：唯一启用场景 `Assets/Game/Scenes/Main.unity`。
- 规模：约 97 个 C# 文件、8,699 行；302 个受版本管理工程文件；3 个场景（其中 2 个为 `_Recovery`），0 Prefab，0 图片/音频/字体/动画/材质/Shader 自有资源。
- 架构：`Assets/Game/Scripts` 为客户端领域层与原型 UI；`Resources/Config` 为 20 份 JSON 配置；`Backend` 为可选权威服务；EditMode/PlayMode 与后端验证程序已存在。
- 工具补充：后续已生成本地 Graphify 图并用于发现境界集成与 Gate 证据缺口；图存在 234 个悬空端点、387 条有向折叠边和 393 条无向折叠边，只作为导航线索，结论均由源码、测试与构建证据复核。

## 2. 系统复用判断

| 系统 | 状态 | 证据与处置 |
|---|---|---|
| 战斗 | READY | `AutoBattleEngine`、`DamageCalculator` 纯 C# 且有 EditMode 测试；保留。 |
| 角色 | READY | 属性、修改器、统一聚合和战力公式存在；保留。 |
| 敌人 | REFACTOR | 怪物配置/工厂存在，但场景没有角色表现或 Prefab；补产品呈现。 |
| Boss | REFACTOR | Boss 配置、狂暴、掉落存在；主场景仅文本表达，且首 Boss 约 12 分钟，不符合 2–4 分钟目标。 |
| 装备 | READY | 生成、品质、随机词条、套装、特殊效果、比较和换装完整且有测试。 |
| 背包 | REFACTOR | 三类背包与分解领域逻辑存在；玩家 UI 仍是一次性文字操作。 |
| 掉落 | READY | 配置化 DropTable、来源和 10,000 次统计测试存在。 |
| 数值/战力 | READY | 统一属性与战力计算存在并测试单调性。 |
| 关卡 | READY | 1-1～1-10 配置链、Boss 关与服务存在。 |
| 地图 | MISSING | 独立地图系统不存在；V0.1 用关卡进度条即可，不新增大地图。 |
| 境界 | TESTING | `bffd195` 已接入真实突破成本、累计修为、Boss 破境石、跨重启渡劫、灵根与统一战力；待当前 Unity 111/28 实跑。 |
| 技能 | REFACTOR | 主动/被动/CD/AOE/DOT 等战斗支持存在；缺少可理解的玩家反馈。 |
| 本地存档 | REFACTOR | 原子写入、版本、SHA-256 仓库存在且单测覆盖，但主场景未调用。 |
| 离线收益 | REFACTOR | 计算/领取与后端接口存在；离线本地闭环及回归测试未接入主场景。 |
| UI/UX | REBUILD | 单场景 UGUI 可操作，但大量页面写明“占位交互”，缺少商业产品层级、装备卡片与战斗表现。保留领域调用，重做信息架构和关键页面。 |
| 音频 | TESTING | 无外部音频资产；已实现五类运行时程序化短音效，待真机听感与音量验收。 |
| 商店 | REFACTOR | 商品、货币、限购与页面入口存在；本地模式行为为硬编码。 |
| 商业化 | REFACTOR | 商品结构、权益服务、订单流程与 Mock Provider 存在；需延后曝光并统一接口。 |
| 支付 | REFACTOR | Development Mock 与 Google Play/抖音占位 Provider 存在；真实 SDK/账号未接。 |
| 广告 | MISSING | V0.1 可不接真实广告，但应保留无广告假入口或明确不启用。 |
| Analytics | MISSING | 只有本地 playtest JSONL；缺少事件接口和关键漏斗定义。 |
| 设置 | MISSING | 未发现音量、震动、隐私或存档操作页面。 |
| 测试 | TESTING | 当前源码预计 EditMode 111、PlayMode 28；静态程序集与离线 smoke PASS，Unity Test Runner 因目标项目会话绑定阻塞尚未运行。 |
| Build Pipeline | TESTING | RC APK/AAB 方法及产物门禁已存在；`2bee3ac` 历史双产物通过，但必须为 `bffd195` 重建。 |

## 3. 修旧与重写比较

| 范围 | 修旧估时 | 重写估时 | 决策 |
|---|---:|---:|---|
| 领域层（战斗/装备/数值/掉落） | 1–2 天集成与修复 | 5–7 天 | 复用 |
| 存档/挂机 | 1 天接线 | 2–3 天 | 复用并重构集成 |
| 主场景 UI/反馈 | 3–4 天 | 3–4 天 | 重做产品层，保留控制器契约 |
| 商业化接口 | 1 天收口 | 3 天 | 复用 |
| 后端 | V0.1 离线优先，不进入关键路径 | 5+ 天 | 保留但不扩张 |

## 4. 关键问题

1. `demo_pacing.json` 的首次掉落约 1 分钟、Boss 约 12 分钟，与任务要求的 20–60 秒掉落和 2–4 分钟 Boss 冲突。
2. `PrototypeGameController` 没有使用 `JsonPlayerSaveRepository`，退出/重进完整循环尚未由当前场景证明。
3. 场景没有自有视觉/音频资产，战斗、掉落和 Boss 主要为文字反馈，商业测试吸引力不足。
4. 既有文档引用的 TestResults、Build 和截图都被 `.gitignore` 排除且没有随工程迁移，必须重新生成当前证据。
5. Backend 的 `.csproj` 曾被全局 ignore；本轮已恢复并对 Backend 添加例外。

## 5. 最短关键路径

`主场景持久状态模型 → 20 秒内自动战斗 → 60 秒内首件装备 → 比较/换装/战力跳字 → 2–4 分钟 Boss 与保底掉落 → 保存退出/重进 → 离线收益 → 商店接口 → Android Release 构建与实机/模拟器 QA`。

后端、排行榜、邮件、复杂任务与 V0.2 系统不阻塞 GATE 1；当前第一优先级是把已有领域逻辑变成陌生玩家能理解且能持续玩的竖屏产品。
