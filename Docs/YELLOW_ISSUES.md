# Yellow Issues

| ID | 范围 | 问题 | 当前降级/替代 | 后续时机 |
|---|---|---|---|---|
| Y-001 | UI/美术 | 角色、怪物、装备图标及部分特效仍为程序化/文字占位 | 核心循环与品质反馈可运行，不依赖外部素材 | 真机首轮后按最高感知缺口替换 |
| Y-002 | 合规 | 隐私政策与用户协议入口可用，但法律主体、邮箱和最终文本未确认 | 候选版明确展示数据范围与草案状态 | 外部测试发布前必须转 RED-人工事项关闭 |
| Y-003 | 音频 | 未发现可确认授权的正式音效资产 | 五类短音效由运行时代码生成，无外部版权依赖 | 真机首轮校准听感与音量 |
| Y-006 | 平衡 | `848365d` 已修复长期轮回无成长与 Boss spam；本条只保留 8 秒狂暴缺专门提示及当前 Unity/真机手感未验 | 离线 RealBattle 10/60 分钟已覆盖递增强度、失败恢复和奖励清空；不把静态结果描述为真机难度已冻结 | 在当前 Unity/真机验收中确认狂暴提示、音画反馈与手感 |
| Y-007 | Development 在线模式 | 未确认的 Finish 会在当前进程内复用同一 session/key 退避重试，但强杀进程会丢失该意图；确定性 4xx 目前也走同一退避 | V0.1 默认本地核心循环，服务器登录入口仅 Development 可见；服务端幂等与当前进程响应丢失已覆盖，问题不阻塞离线商业验证包 | 对外启用权威服务器登录前，持久化 pending intent，并区分 definitive 4xx、transient 与 unknown outcome |
| Y-008 | UI 验收 | batch UI 结构审计 0 issue，但未捕获真实屏幕截图，`visualAuditComplete=false` | 不把历史 26/26 PlayMode 或结构审计描述成当前完整视觉通过 | QA-DEVICE-001 真机阶段完成 9:16、字体、触控和遮挡清单 |
| Y-009 | Android 签名 | `2bee3ac` 历史 RC APK/AAB 使用 Unity 默认 Android 测试签名，且已不对应当前源码 | 仅作历史安装参考，不作为 `1370ee6` 或后续源码的当前 RC/商店产物 | 当前源码全绿后重建；商店上传前配置独立 upload/release keystore 并复验 |
| Y-010 | Development 在线会话 | async 结算在场景销毁后仍缺少 generation guard/网络超时 | 后端 exact-current-stage、单活动会话、并发幂等、失联恢复和旧库升级已回归；正式 RC 仍隐藏服务器入口 | 对外开放在线入口前补延迟响应/场景重载测试、请求超时，并持久化客户端 pending intent |
| Y-011 | 测试覆盖 | UnityWebRequest 客户端到真实服务端的同进程/设备联调证据尚未覆盖 | 真实 Kestrel HTTP 46/46 已覆盖服务端路由、鉴权、累计修为/破境石/pending profile、拒绝零副作用、inventory 与 battle 契约；109/26 是 `2bee3ac` 历史证据，当前 Unity 未运行 | 对外启用 Development 在线入口或真机联调时补 UnityWebRequest 端到端证据，不阻塞离线商业验证 |
| Y-012 | Unity 自动化环境 | 许可证有效，但当前 Hub 授权主 Editor 打开另一项目；目标仓库的版本化/通用 IPC 与直接交互替代路径未取得同一授权上下文 | `QA-UNITY-POSTCHANGE-002` 标记 BLOCKED；停止重复失败命令，保留其他 Workstream 运行 | 目标项目出现可复用授权 Editor/Licensing 会话时立即重跑当前完整测试（现静态枚举 125/34）和 Android 构建 |
| Y-013 | Development 在线境界失败政策 | `ca495ef` 已完成破境石、RequiredLevel、按阶损失与 pending→Boss 胜利结算；服务器尚未持久化 300 秒小突破失败冷却，也未定义 Boss 失败/显式放弃的 75% 退款结算入口 | V0.1 默认离线循环且在线入口仅 Development 可见；当前 major pending 在 Boss 失败后保留供重试，不伪造退款或取消 | 对外启用在线入口前确定失败/放弃产品政策并实现 cooldown、退款、并发回归 |
| Y-014 | 损坏在线 pending 恢复 | `ca495ef` 已将状态分为 Empty/Valid/Corrupt，Corrupt 不会覆盖/二次扣料，也不阻断 Boss 普通结算，但目前只能隔离、不能自动判定是否应退款 | Profile 只暴露 Valid；Corrupt 的突破请求无副作用拒绝，避免猜测性清除或增发资源 | 对外在线运营前增加受审计的客服/管理员恢复流程，不阻塞离线 RC |
| Y-015 | Development 在线跨轮回 | `848365d` 的持久轮回与递增结算仅在离线 RC 路径生效；Development 在线仍固定 cycle 1，服务端未权威持久化轮回序号、轮回计时或按完成轮回结算奖励 | Release 离线 RC 隐藏服务器入口并保持完整本地循环；不伪装在线跨轮回已支持，也不阻塞商业验证包 | 对外开放在线入口前，在服务端持久化当前轮回/开始时间并以已完成轮回权威结算、补迁移与并发回归 |
| Y-016 | GitHub 里程碑同步 | 本地 `main=1370ee6`，本地跟踪的 `origin/main=0ffbe55`；三次本地提交因 `github.com:443` 间歇 TCP 超时尚未 Push | 已确认不是认证或仓库权限错误；保留本地 Git 检查点并将 `GITHUB-SYNC-002` 标记 BLOCKED，其他 Workstream 继续 | 网络恢复后重试 Push，随后用远端引用验证 `main` 与本地一致；若演变为权限/账号操作再升级 RED |
