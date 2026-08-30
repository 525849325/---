# Implementation Plan: 《太初：无尽轮回》V0.1

## Overview

在现有《太初拾遗录》领域层上完成商业验证版，不扩展 MMO 系统。优先交付战斗→掉落→换装→战力→Boss 的竖屏可玩切片，再接入持久化、离线收益、商店接口与 Android RC。

## Architecture Decisions

- 保留纯 C# 领域服务和 JSON 配置，重构 Main 场景的产品层。
- V0.1 离线可玩，后端为可选增强，不作为 GATE 1 启动依赖。
- 所有关键进度进入一个版本化 `PlayerSaveSnapshot`；UI 不直接制造奖励。
- 视觉采用程序化 2D 最小资产路线，避免等待外部美术。

## Task List

当前权威任务状态见 `Docs/TASK_QUEUE.md`，验收真相见 `Docs/MVP_ACCEPTANCE.md`；`tasks/todo.md` 仅保留启动时的历史拆解，日程与并行流见 `Docs/SPRINT_14D.md`。

## Risks and Mitigations

| 风险 | 影响 | 缓解 |
|---|---|---|
| 历史报告与当前证据脱节 | 高 | 全部测试/构建本轮重跑，不继承结论 |
| 无美术音频 | 高 | 程序化视觉，Day 6 冻结最小风格 |
| 主场景状态分散 | 高 | 先建立统一运行状态与存档契约 |
| Android 签名/账号 | 中 | Mock/Development 不停工，人工项 Day 10 前处理 |

## Open Questions

无阻塞性产品问题；包名、签名、渠道和法律文本已记录到人工事项。
