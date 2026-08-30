# V0.1 资产清单

审计日期：2026-08-30。分类：`DIRECT_USE`、`NEEDS_EDIT`、`PLACEHOLDER`、`UNUSABLE`。

| 类别 | 数量/位置 | 分类 | 说明 |
|---|---|---|---|
| 主场景 | `Assets/Game/Scenes/Main.unity` | NEEDS_EDIT | 可运行 UGUI 骨架，需重做战斗区、装备卡、导航与设置。 |
| Recovery 场景 | `Assets/_Recovery/*.unity` 2 个 | UNUSABLE | Unity 恢复文件，不进入 Build。 |
| 角色图像/模型 | 0 | PLACEHOLDER | 需要低成本程序化剪影/图形或合规素材。 |
| 普通怪物 | 0 | PLACEHOLDER | 配置有“荒原妖兽”，无视觉资产。 |
| Boss | 0 | PLACEHOLDER | 配置有“石魇”，无视觉资产与专属登场表现。 |
| 装备图标 | 0 | PLACEHOLDER | 先用槽位形状、品质色和符号生成，后续替换。 |
| UI 图像/Prefab | 0 | NEEDS_EDIT | 当前 UI 全部嵌在 Main 场景或由 Editor 脚本生成。 |
| 背景 | 0 | PLACEHOLDER | 建议程序化渐变/山峦剪影，避免等待美术。 |
| 特效 | 0 | PLACEHOLDER | 用 UGUI tween、屏幕闪烁、粒子基础形状实现。 |
| 音效/音乐 | 0 个外部文件；5 类程序化短音效 | NEEDS_EDIT | 攻击、掉落、换装、Boss、突破由运行时代码生成，无外部版权依赖；待真机听感与音量验收。 |
| 字体 | Unity 内置 LegacyRuntime | NEEDS_EDIT | 可开发使用；中文发布需确认 Android 字形完整并引入有授权字体。 |
| 动画/Animator | 0 | PLACEHOLDER | 用程序化位移/缩放先完成反馈。 |
| Material/Shader | 0 自有 | DIRECT_USE | Built-in UI 默认 Shader 足够 V0.1。 |
| JSON 数值配置 | `Assets/Game/Resources/Config/*.json` 20 份 | DIRECT_USE | 怪物、Boss、装备、品质、词条、关卡、境界、商店等可调。 |
| Editor 工具 | `Assets/Game/Editor` | NEEDS_EDIT | 场景生成、RC APK/AAB 构建方法和规格测试已存在；当前静态枚举 116/33，仍需补 Git SHA 溯源、旧产物清理及完整门禁后重建双产物。 |

## 资产策略

GATE 1 不等待外部美术：用程序化 2D 形状、品质色、屏幕震动、数值跳字和装备卡建立反馈。Day 6 前若仍无合规素材，则固化这一“水墨剪影 + 霓虹品质光”的最小美术方向；不得抓取来源不明资源。
