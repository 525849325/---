# 《太初：无尽轮回》V0.1 MVP 验收矩阵

审计日期：2026-08-30。当前源码基线：`6597769`。状态只接受与当前源码对应的直接证据；更早提交的测试、构建、模拟器截图只保留为历史参考，不自动继承为当前 PASS。

状态定义：`PASS` 为当前源码已有直接自动化或运行证据；`TESTING` 为实现已落地但当前 Unity/设备证据未闭合；`BLOCKED` 为存在外部或环境依赖；`PARTIAL` 为功能可用但发布边界尚未完成。

## 商业验证核心循环

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 启动后进入离线自动战斗 | TESTING | 当前程序集静态编译与核心循环 smoke PASS；需 `6597769` 对应 PlayMode 运行。 |
| 20–60 秒首件装备、比较/换装、战力增长 | TESTING | 10/60 分钟领域和真实战斗节奏 smoke PASS；当前 Unity 场景与真机触控未复验。 |
| 2–4 分钟首 Boss、奖励窗口和循环推进 | TESTING | 首 Boss 约 182 秒到达、193 秒击败；离线 smoke 奖励账一致，待当前 PlayMode 与设备确认。 |
| 保存、重进与离线收益 | TESTING | 聚合存档、原子写入、损坏隔离、离线单次领取和累计修为旧档迁移已有实现与离线证据；待当前 Unity 重进回归。 |
| 境界、破境石、渡劫、灵根和统一战力 | TESTING | `6597769` 已接入离线真实突破/Boss/pending/灵根，并对齐在线累计修为 profile；静态编译、领域与后端回归 PASS，新增 Unity 用例尚未运行。 |
| 10/60 分钟节奏与不死锁 | PASS | 当前 CoreLoop、RealBattle 与 Balance smoke 均 PASS；该结论不替代真机性能与触控验收。 |
| 商店结构与 Development Mock | PARTIAL | 商品结构、成长后曝光、Release 拒绝 Mock 与设置入口已实现；真实渠道账号、商品与签名不属于自动化闭环。 |

## 工程、安全与后端

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| Unity 必需目录和 `.meta` 纳入 Git | PASS | `Assets/`、`Packages/`、`ProjectSettings/` 与 `Docs/` 已版本管理。 |
| 生成目录与本地缓存排除 | PASS | Unity Library/Temp/Logs/obj/Build 缓存按 `.gitignore` 排除。 |
| 敏感信息与大文件门禁 | PASS | 当前扫描未发现真实密码、API Key、Token、私钥、证书或签名文件；无新增 50 MB 文件。 |
| 后端权威战斗与真实 HTTP 合同 | PASS | Backend Verification 与真实 Kestrel HTTP 40/40 PASS；`cultivationExperience` 为兼容性附加字段，既有合同无回归。 |
| Development 在线境界语义 | PARTIAL | 等级经验/累计修为、旧库迁移、奖励和 profile/Unity 映射已对齐；破境石、RequiredLevel 与 pending→Boss 仍记录为 `SERVER-REALM-MATERIAL-002` / Y-013。 |
| P0/P1 范围冻结 | TESTING | 当前未新增非发布必需功能；仍需关闭境界运行时、Android 重建、竖屏视觉和真机 P0。 |

## 测试、构建与发布 Gate

| 要求 | 状态 | 当前证据 / 缺口 |
|---|---|---|
| 当前 Unity EditMode | BLOCKED | 预计 111 项；尚未在 `6597769` 运行。`2bee3ac` 的 109/109 仅为历史证据。 |
| 当前 Unity PlayMode | BLOCKED | 预计 28 项；尚未在 `6597769` 运行。`2bee3ac` 的 26/26 仅为历史证据。 |
| 当前 Android RC APK | BLOCKED | 旧 APK 对应 `2bee3ac`，已过期；必须在当前 Unity 全绿后重建。 |
| 当前 Android RC AAB | BLOCKED | 旧 AAB 对应 `2bee3ac`，已过期；必须重建并复验 metadata、ABI、zipalign/signature 或 bundletool。 |
| 1080×1920 九页视觉验收 | BLOCKED | 目前只有结构性 UI 证据，没有当前完整竖屏截图集。 |
| Android 物理真机 10/60 分钟 | BLOCKED | 当前 ADB 为 0 台已授权物理设备；没有有效豁免，也不得以模拟器代替。 |
| 正式商店签名 | PARTIAL | 历史 RC 为 Unity 默认测试签名；商店发布需要独立 upload/release keystore。 |

## 当前结论

V0.1 核心源码已经进入 RC-candidate，但 **MVP 尚未完成，GATE 4 保持 OPEN**。当前不能用历史 109/26、历史 APK/AAB、模拟器截图或旧“真机豁免”给 `6597769` 放行。放行顺序固定为：当前 EditMode 111 → PlayMode 28 → APK → AAB → 1080×1920 视觉 → 物理真机 10/60 分钟与日志/性能验收。
