# V0.1 可执行任务

## Task 1：TASK-000 审计与安全基线

**Acceptance criteria:**
- [x] 安全检查点存在。
- [x] 工程、系统、资产、14 日计划、Backlog、人工事项已记录。

**Verification:** `git log -1`；核对 `Docs/PROJECT_AUDIT.md` 等文档。  
**Dependencies:** None。 **Scope:** M。

## Task 2：GATE 1 前 4 分钟可玩竖切片

**Acceptance criteria:**
- [ ] 启动后 20 秒内可见自动战斗，60 秒内首掉落。
- [ ] 装备比较/换装带来可见战力上涨。
- [ ] 2–4 分钟出现 Boss，胜利提供高品质保底奖励。

**Verification:** EditMode 节奏测试；Main PlayMode 场景测试；人工 4 分钟试玩。  
**Dependencies:** Task 1。 **Files likely touched:** pacing config、Main 控制器、场景、测试。 **Scope:** M（按竖切片拆分提交）。

## Checkpoint：GATE 1

- [ ] Unity 编译、EditMode、PlayMode 全绿。
- [ ] 场景核心循环端到端可玩。

## Task 3：存档与离线完整循环

**Acceptance criteria:**
- [ ] 统一快照保存关卡、装备、货币、境界和时间戳。
- [ ] 退出重进恢复；损坏存档安全降级。
- [ ] 离线收益按上限结算且不可重复领取。

**Verification:** 单元测试、场景重载测试、Android 生命周期检查。  
**Dependencies:** Task 2。 **Scope:** M。

## Task 4：商业化结构与设置

**Acceptance criteria:**
- [ ] 商品/礼包读取配置，成长价值建立后才显示入口。
- [ ] Mock 支付走统一 Provider，不由 UI 直接发币。
- [ ] 音量、震动、隐私/协议入口和存档操作可用。

**Verification:** EditMode + PlayMode；Production 配置拒绝 Mock。  
**Dependencies:** Task 3。 **Scope:** M。

## Task 5：Android Release Candidate

**Acceptance criteria:**
- [ ] Release APK 或 AAB 构建成功并可安装。
- [ ] P0/P1 为 0，核心流程和存档稳定。
- [ ] 10 分钟与 60 分钟测试证据完整。

**Verification:** 质量门禁、安装/启动、真机或明确记录的外部阻塞。  
**Dependencies:** Task 2–4。 **Scope:** M。
