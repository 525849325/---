# 《太初：无尽轮回》V0.1 MVP 验收矩阵

审计日期：2026-08-31。当前本地代码检查点：`1370ee6`；本地跟踪的 `origin/main`：`0ffbe55`。状态只接受与当前源码绑定的直接证据；更早测试、构建和截图不自动继承为 PASS。

状态：`PASS` 当前源码已有直接证据；`TESTING` 已实现但运行/产品验收未闭合；`BLOCKED` 依赖外部环境；`PARTIAL` 最小能力存在但发布边界未完成。

## 商业验证核心循环

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 启动后进入离线自动战斗 | TESTING | 静态编译与 smoke PASS；待当前 PlayMode/设备运行。 |
| 20–60 秒首件装备、比较/换装、战力增长 | TESTING | Domain/Core/RealBattle 证据存在；待当前 Unity 场景与真机触控复验。 |
| 2–4 分钟首 Boss、奖励窗口 | TESTING | 首 Boss 182.23 秒到达、193.43 秒击败；待当前 Unity/设备。 |
| 首 Boss 后更高区域与 8–10 分钟成长阻力 | TESTING | `848365d` 已实现持久轮回、节奏门重置和敌人/奖励递增；RealBattle 第二 Boss 496.07/505.87 秒，10 分钟 2 胜无第三，60 分钟 11 胜6败 pending 0，120 分钟后继续；待当前 Unity/真机。 |
| 保存、重进与离线收益 | TESTING | 聚合存档、原子写入、损坏隔离、离线单次领取与迁移已有离线证据；`5a61ddf` 证明可恢复写入失败不杀死循环；待当前 Unity 重进。 |
| 境界、破境石、渡劫、灵根与统一战力 | TESTING | 离线及 Development 在线材料/pending→Boss 合同已闭合，HTTP 46/46；待 Unity 125/34。 |
| 产品页可理解且无技术占位 | TESTING | `1370ee6`：12/12 页为真实只读摘要，占位扫描 0，离线导航不发奖励、不写存档/漏斗；在线假状态和 offline 商店文案经两路独立审查 APPROVE；待 Unity/9:16 视觉。 |

## 商业验证与合规

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 商店结构与 Release 支付安全 | PARTIAL | 商品合同、成长后曝光、Development Mock 与 Production 拒绝 Mock 已实现；尚缺可测的商品选择/购买意图漏斗。 |
| 隐私同意与数据发送门 | PARTIAL | `PRIVACY-CONSENT-001` RUNNING；法律入口存在，但仍需明确接受/拒绝并在同意前关闭分析与 Development 登录数据发送。 |
| 任务/留存语义 | TESTING | 已诚实降级为“一次性成长试炼”，重载防重复测试源码通过且独立审查 APPROVE；待当前 Unity。 |
| 漏斗事件 | PARTIAL | 本地无 PII JSONL 及成长事件存在；需补 shop_opened/product_selected/purchase_intent/result。 |

## 后端与质量证据

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 后端权威战斗与真实 HTTP 合同 | PASS | Backend Verification 与真实 Kestrel HTTP 46/46 PASS。 |
| Development 在线境界语义 | PASS | 双经验池、破境石、RequiredLevel、按阶成本、pending→Boss、灵根、任务、旧库升级与客户端防重已闭合。 |
| Development 在线跨轮回 | PARTIAL | 当前仍固定 cycle 1，服务端未权威持久化/结算跨轮回状态；Release 离线 RC 隐藏入口，后续见 Y-015 / V0.2。 |
| 存档写入临时失败恢复 | PASS | `5a61ddf`：预期 I/O/权限失败降级，循环继续，后续保存重试并清除警告；编程错误不被吞掉。 |
| 当前 Unity EditMode | BLOCKED | 预计 125 项；尚未运行。`2bee3ac` 的 109/109 仅为历史证据。 |
| 当前 Unity PlayMode | BLOCKED | 预计 34 项；尚未运行。`2bee3ac` 的 26/26 仅为历史证据。 |
| Android APK/AAB | BLOCKED | 旧 `2bee3ac` 产物 STALE；需构建前清旧、绑定最终 Git SHA 并验证包信息/ABI/签名/Manifest。 |
| 1080×1920 十二页视觉 | BLOCKED | 当前没有完整真实截图证据；结构审计不等于视觉 PASS。 |
| 物理真机 10/60 分钟 | BLOCKED | 当前 0 台已授权设备；脚本/清单已就绪，必须使用同源 APK。 |

## Gate 结论

V0.1 已进入 RC-candidate 开发后段，但 **MVP 尚未完成，GATE 4 保持 OPEN**。当前执行顺序：先收口 `PRIVACY-CONSENT-001` 等可执行 P1，再在目标 Unity 会话可用时运行完整 EditMode/PlayMode，生成同源 APK/AAB，完成 12 页竖屏与物理真机 10/60 分钟验收。GitHub 网络同步阻塞不停止开发；历史 109/26、旧产物、模拟器截图或旧真机豁免均不得用于放行。
