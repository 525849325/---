# Yellow Issues

| ID | 范围 | 问题 | 当前降级/替代 | 后续时机 |
|---|---|---|---|---|
| Y-001 | UI/美术 | 角色、怪物、装备图标及部分特效仍为程序化/文字占位 | 核心循环与品质反馈可运行，不依赖外部素材 | 真机首轮后按最高感知缺口替换 |
| Y-002 | 合规 | 隐私政策与用户协议入口可用，但法律主体、邮箱和最终文本未确认 | 候选版明确展示数据范围与草案状态 | 外部测试发布前必须转 RED-人工事项关闭 |
| Y-003 | 音频 | 未发现可确认授权的正式音效资产 | 五类短音效由运行时代码生成，无外部版权依赖 | 真机首轮校准听感与音量 |
| Y-006 | 平衡 | 生产 Controller 为保证演示连贯把玩家 HP 下限固定为 9999，真实战斗模型因此 10/60 分钟均为 0 次失败 | 关卡失败重试已有领域与流程覆盖；当前模型只验证 Boss 时点、吞吐、奖励账与不死锁，不声称难度已平衡 | 最新 Android 真机 10/60 分钟验收后决定 V0.1 是否降低保胜阈值 |
| Y-007 | Development 在线模式 | 未确认的 Finish 会在当前进程内复用同一 session/key 退避重试，但强杀进程会丢失该意图；确定性 4xx 目前也走同一退避 | V0.1 默认本地核心循环，服务器登录入口仅 Development 可见；服务端幂等与当前进程响应丢失已覆盖，问题不阻塞离线商业验证包 | 对外启用权威服务器登录前，持久化 pending intent，并区分 definitive 4xx、transient 与 unknown outcome |
| Y-008 | UI 验收 | batch UI 结构审计 0 issue，但未捕获真实屏幕截图，`visualAuditComplete=false` | 不把 26/26 PlayMode 或结构审计描述成完整视觉通过 | QA-DEVICE-001 真机阶段完成 9:16、字体、触控和遮挡清单 |
| Y-009 | Android 签名 | RC APK/AAB 使用 Unity 默认 Android 测试签名 | 当前产物可安装并用于商业验证，但不直接上架 | 商店上传前配置独立 upload/release keystore，并重新构建验证 |
| Y-010 | Development 在线会话 | async 结算在场景销毁后缺少 generation guard/网络超时；后端 battle start 只校验直接前置，不强制等于 profile 当前关 | 正式 RC 隐藏服务器入口；正常单场景流程、幂等与 profile 原子镜像回归全绿 | 对外开放在线入口前补延迟响应/场景重载测试、请求超时与服务端 exact-current-stage 校验 |
| Y-011 | 测试覆盖 | 真实 HTTP + UnityWebRequest 端到端契约、非法刷新 profile 保持旧镜像的负向测试尚未覆盖 | Fake DTO 与后端契约一致；EditMode 109/109、PlayMode 26/26、Backend Verification PASS | RC-QUALITY-001 后续按风险补最小契约测试，不阻塞离线商业验证 |
