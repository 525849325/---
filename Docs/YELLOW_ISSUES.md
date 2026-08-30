# Yellow Issues

| ID | 范围 | 问题 | 当前降级/替代 | 后续时机 |
|---|---|---|---|---|
| Y-001 | UI/美术 | 角色、怪物、装备图标及部分特效仍为程序化/文字占位 | 核心循环与品质反馈可运行，不依赖外部素材 | 真机首轮后按最高感知缺口替换 |
| Y-002 | 合规 | 隐私政策与用户协议入口可用，但法律主体、邮箱和最终文本未确认 | 候选版明确展示数据范围与草案状态 | 外部测试发布前必须转 RED-人工事项关闭 |
| Y-003 | 音频 | 未发现可确认授权的正式音效资产 | 五类短音效由运行时代码生成，无外部版权依赖 | 真机首轮校准听感与音量 |
| Y-006 | 平衡 | 生产 Controller 为保证演示连贯把玩家 HP 下限固定为 9999，真实战斗模型因此 10/60 分钟均为 0 次失败 | 关卡失败重试已有领域与流程覆盖；当前模型只验证 Boss 时点、吞吐、奖励账与不死锁，不声称难度已平衡 | 最新 Android 真机 10/60 分钟验收后决定 V0.1 是否降低保胜阈值 |
| Y-007 | Development 在线模式 | 未确认的 Finish 会在当前进程内复用同一 session/key 退避重试，但强杀进程会丢失该意图；确定性 4xx 目前也走同一退避 | V0.1 默认本地核心循环，服务器登录入口仅 Development 可见；服务端幂等与当前进程响应丢失已覆盖，问题不阻塞离线商业验证包 | 对外启用权威服务器登录前，持久化 pending intent，并区分 definitive 4xx、transient 与 unknown outcome |
| Y-008 | UI 验收 | batch UI 结构审计 0 issue，但未捕获真实屏幕截图，`visualAuditComplete=false` | 不把历史 26/26 PlayMode 或结构审计描述成当前完整视觉通过 | QA-DEVICE-001 真机阶段完成 9:16、字体、触控和遮挡清单 |
| Y-009 | Android 签名 | `2bee3ac` 历史 RC APK/AAB 使用 Unity 默认 Android 测试签名，且已不对应当前源码 | 仅作历史安装参考，不作为 `6597769` 当前 RC 或商店产物 | 当前源码全绿后重建；商店上传前配置独立 upload/release keystore 并复验 |
| Y-010 | Development 在线会话 | async 结算在场景销毁后仍缺少 generation guard/网络超时 | 后端 exact-current-stage、单活动会话、并发幂等、失联恢复和旧库升级已回归；正式 RC 仍隐藏服务器入口 | 对外开放在线入口前补延迟响应/场景重载测试、请求超时，并持久化客户端 pending intent |
| Y-011 | 测试覆盖 | UnityWebRequest 客户端到真实服务端的同进程/设备联调证据尚未覆盖 | 真实 Kestrel HTTP 40/40 已覆盖服务端路由、鉴权、累计修为 profile、inventory、battle 成功/拒绝/幂等契约；109/26 是 `2bee3ac` 历史证据，当前 Unity 未运行 | 对外启用 Development 在线入口或真机联调时补 UnityWebRequest 端到端证据，不阻塞离线商业验证 |
| Y-012 | Unity 自动化环境 | 许可证有效，但当前 Hub 授权主 Editor 打开另一项目；目标仓库的版本化/通用 IPC 与直接交互替代路径未取得同一授权上下文 | `QA-UNITY-POSTCHANGE-002` 标记 BLOCKED；停止重复失败命令，保留其他 Workstream 运行 | 目标项目出现可复用授权 Editor/Licensing 会话时立即重跑 111/28 和 Android 构建 |
| Y-013 | Development 在线境界 | `6597769` 已分离并端到端暴露累计修为；后端仍以软币代替破境石、未校验 RequiredLevel，且大境界立即晋升发灵根，没有 pending→Boss 渡劫/退款 | V0.1 RC 默认离线循环；在线入口仅 Development 可见，累计修为判定与显示已权威一致 | `SERVER-REALM-MATERIAL-002` 在不扩大 V0.1 离线范围的前提下迁移材料与渡劫合同 |
| Y-014 | 损坏存档韧性 | 极端损坏 pending 若带合法但非下一境界目标，可能在不可由正常流程产生的状态下阻塞或跳过 | 当前已在扣资源、清 token 前校验目标、费用、经验与余额；正常流程和常见损坏均不变异 | 存档恢复策略版本化时增加 next-target 严格校验与隔离测试 |
