# ImmortalLoot Server

ASP.NET Core 8 + EF Core 的服务器权威后端。开发默认使用 SQLite；`GameDbContext` 的实体和索引保持 PostgreSQL 可迁移性，Redis 缓存将在排行榜阶段接入。

## 运行

```powershell
$dotnet = 'C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Data\DotNetSdk\dotnet.exe'
& $dotnet restore .\Backend\src\ImmortalLoot.Server\ImmortalLoot.Server.csproj
& $dotnet run --project .\Backend\src\ImmortalLoot.Server\ImmortalLoot.Server.csproj
& $dotnet run --project .\Backend\tests\ImmortalLoot.Server.Verification\ImmortalLoot.Server.Verification.csproj
```

真实接口另包含 `GET /rankings/{Power|Realm|Stage}`，支持 UTC 周期、分页和自身名次。排行榜分数只从服务器存档生成，不接受客户端上传。

`GET /player/profile` 同时返回 `currentStageId` 与按配置顺序归一化的 `clearedStageIds`。客户端进入在线玩法前必须先取得该权威关卡快照与 `/player/inventory`，不得用本地离线存档决定在线关卡或背包。

`POST /battle/finish` 保留可选的 `RewardWindowEligible` 字段以兼容旧客户端，但当前服务器不会信任该客户端字段。普通关只记录权威通关与首通奖励；Boss 奖励由服务器关卡配置强制决定。普通关计时掉落只有在奖励时钟改为服务器持久化后才可重新开放。

支付默认注入 `RejectingPaymentReceiptVerifier`，不会接受任何真实付款回执。部署前必须为目标商店实现并注册 `IPaymentReceiptVerifier`，不得在客户端或未验签的 webhook 中直接发货。

排行榜支持 `period=permanent` 与 `period=weekly`；默认永久榜。`IRankingCache` 当前为单进程内存实现，生产多实例应替换为 Redis。活动 `activity_double_afk_launch` 的倍率由服务器挂机服务读取，不依赖客户端时钟。

开发数据库通过 `EnsureCreated` 初始化。正式部署 PostgreSQL 前应改用受版本控制的 EF Core migrations；SQLite 数据库文件只用于本地开发，不应提交。
