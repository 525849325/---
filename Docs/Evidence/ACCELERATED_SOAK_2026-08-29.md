# Development Player 加速长稳测试 — 2026-08-29

此证据验证真实 Windows Development Player 的完整 120 分钟逻辑时轴、资源压力与自动退出；它不替代真人对体验质量的主观结论。

## 执行方式

```powershell
Build\Windows\ImmortalLoot.exe -batchmode -nographics -playtestSpeed=120 -playtestAutoQuit
```

- Player 构建大小：154,145,164 bytes
- 配置模拟时长：7,200 秒
- 实际运行时长：61.80 秒（等待最后奖励窗口清空后退出）
- 自动退出：是，退出前写入 `session_end`
- 五分钟采样：24/24（模拟 300 秒至 7,200 秒）

## 完成状态

Player 日志终态：

```text
PLAYTEST_COMPLETE elapsed=7200 kills=359 rewardWindows=286 consumed=286 pending=0 inventory=120 power=1181
```

| 指标 | 结果 |
|---|---:|
| 击杀 | 359 |
| 配置奖励窗口 | 286 生成 / 286 消费 / 0 待处理 |
| 装备背包 | 120 / 120（有界） |
| 最终战力 | 1,181 |
| 采样内存最低 | 73.89 MB |
| 采样内存最高 | 73.92 MB |
| Exception / Error / Crash / Assertion | 0 |

原始遥测由 Development Player 写入：

`%USERPROFILE%\AppData\LocalLow\ImmortalLoot Studio\太初拾遗录\immortal-loot-playtest.jsonl`

原始 Player 日志位于工作区 `windows-player-soak.log`。该日志与测试结果属于生成证据，不参与运行时代码。
