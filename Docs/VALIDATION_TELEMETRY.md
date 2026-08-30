# V0.1 商业验证漏斗

本地验证事件写入 `Application.persistentDataPath/validation-funnel.jsonl`。每行是独立 JSON，不记录玩家 ID、昵称、设备标识、Token、订单或其他个人信息。未来接入第三方 Analytics 时替换 `IValidationEventSink`，玩法层事件契约保持不变。

## 要回答的问题

1. 陌生玩家是否进入游戏并立即看到战斗？
2. 玩家多久获得首件装备，并是否完成首次换装、获得可见战力增量？
3. 玩家多久击败首个 Boss、完成首次境界突破？
4. 商店是否只在装备成长价值建立后曝光？

## 事件

| 事件 | 含义 | 关键字段 |
|---|---|---|
| `session_started` | 一次客户端会话启动 | sessionId、utc |
| `battle_visible` | 战斗主循环已进入可见状态 | elapsedSeconds、stage、power |
| `first_equipment_drop` | 本会话第一次装备掉落 | elapsedSeconds、stage、itemQuality |
| `first_equipment_equipped` | 本会话第一次换装 | power、value（战力增量） |
| `first_boss_defeated` | 本会话第一次 Boss 胜利 | elapsedSeconds、stage、itemQuality |
| `first_realm_breakthrough` | 本会话第一次境界突破 | elapsedSeconds、value（境界阶数） |
| `shop_exposed` | 成长价值建立后首次显示商店入口 | elapsedSeconds、stage、power |

同名里程碑在单次会话内最多记录一次，所有事件携带随机会话关联 ID。写入失败只产生 Warning，不得中断游戏。
