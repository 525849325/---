using ImmortalLoot.Server.Persistence;
using ImmortalLoot.Server.Services;
using ImmortalLoot.Server.Config;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options;
await using var db = new GameDbContext(options);
await db.Database.EnsureCreatedAsync();

var clock = new FixedClock();
var catalog = ServerGameConfigCatalog.LoadDefault();
var legacyBattleFinishRequest = System.Text.Json.JsonSerializer.Deserialize<BattleFinishRequest>(
    "{\"SessionId\":\"00000000-0000-0000-0000-000000000000\",\"IdempotencyKey\":\"legacy-request\"}");
Require(legacyBattleFinishRequest?.RewardWindowEligible is null, "legacy battle finish JSON no longer defaults the additive reward-window field");
Require(catalog.Realms.Count == 10 && catalog.Stages.Count == 10 && catalog.SpiritualRoots.Count == 9 && catalog.CommercialProducts.Count == 6 && catalog.DropTables.Count == 4, "shared Unity JSON catalog did not load expected authoritative rows");
Require(catalog.Tasks.Count == 6 && catalog.ActivityChests.Count == 5 && catalog.Afk.MaximumOfflineHours == 8, "shared task or AFK config did not load expected authoritative rows");
var mockVerifier = new DevelopmentPaymentReceiptVerifier(catalog);
var mockReceipt = await mockVerifier.VerifyAsync("mock", "mock-receipt:IL-DEMO:jade_60", CancellationToken.None);
var malformedReceipt = await mockVerifier.VerifyAsync("mock", "forged", CancellationToken.None);
Require(mockReceipt.Valid && mockReceipt.ProviderTransactionId == "IL-DEMO" && mockReceipt.AmountMinorUnits == 600 && !malformedReceipt.Valid, "development mock receipt verifier is incorrect");
var currencies = new CurrencyService(db);
var rewards = new RewardService(db, currencies);
var tasks = new TaskService(db, rewards, clock, catalog);
var auth = new AuthService(db, clock, tasks);
var firstLogin = await auth.LoginAsync("guest", "device-001", "云游客", CancellationToken.None);
var repeatedLogin = await auth.LoginAsync("guest", "device-001", "ignored", CancellationToken.None);
Require(firstLogin.IsNewPlayer && !repeatedLogin.IsNewPlayer, "login creation state is incorrect");
Require(firstLogin.PlayerId == repeatedLogin.PlayerId, "repeated login must load the same player save");
Require(await auth.ResolvePlayerAsync("Bearer " + firstLogin.AccessToken, CancellationToken.None) == firstLogin.PlayerId, "access token did not resolve player");
Require(await auth.ResolvePlayerAsync("Bearer invalid", CancellationToken.None) is null, "invalid access token was accepted");
var profile = await new PlayerQueryService(db, catalog).GetProfileAsync(firstLogin.PlayerId, CancellationToken.None);
Require(profile.Nickname == "云游客" && profile.Level == 1 && profile.RealmStage == 1, "default player profile was not persisted");

var account = new Account { ExternalAccountId = "verify", Provider = "test" };
var player = new Player { AccountId = account.Id, Nickname = "Verifier" };
db.Accounts.Add(account);
db.Players.Add(player);
await db.SaveChangesAsync();

var equipmentDrops = new ServerEquipmentDropService(db, clock, catalog);
var service = new BattleAuthorityService(db, clock, currencies, tasks, equipmentDrops);
var firstStart = await service.StartAsync(player.Id, "stage_1_1", "start-key", CancellationToken.None);
var repeatedStart = await service.StartAsync(player.Id, "stage_1_1", "start-key", CancellationToken.None);
Require(firstStart.SessionId == repeatedStart.SessionId, "battle start must be idempotent");

var firstFinish = await service.FinishAsync(player.Id, firstStart.SessionId, "finish-key", CancellationToken.None);
var repeatedFinish = await service.FinishAsync(player.Id, firstStart.SessionId, "finish-key", CancellationToken.None);
Require(!firstFinish.Replayed && repeatedFinish.Replayed, "battle finish replay state is incorrect");
Require(firstFinish.RewardExp == 25 && repeatedFinish.RewardExp == firstFinish.RewardExp, "battle experience reward is missing or replay-unstable");
Require(firstFinish.EquipmentInstanceId.Length == 32 && repeatedFinish.EquipmentInstanceId == firstFinish.EquipmentInstanceId, "battle equipment drop must be server generated and replay stable");
Require(await db.PlayerEquipment.CountAsync(value => value.PlayerId == player.Id && value.InstanceId == firstFinish.EquipmentInstanceId) == 1, "battle equipment drop was duplicated or missing");
var configuredNormalDrop = await db.PlayerEquipment.SingleAsync(value => value.InstanceId == firstFinish.EquipmentInstanceId);
Require(configuredNormalDrop.BaseId == "weapon_cloudsteel_blade" && new[] { "Fine", "Rare", "Epic" }.Contains(configuredNormalDrop.Quality), "normal battle equipment ignored the configured drop table or quality bounds");
await RequireThrows<InvalidOperationException>(() => service.StartAsync(player.Id, "stage_1_3", "locked-stage", CancellationToken.None), "locked stage battle was accepted");
Require((await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == player.Id)).SoftCurrency == 10, "reward was duplicated or missing");
Require(await db.RewardGrants.CountAsync() == 1, "reward grant must be unique");
Require(await db.CurrencyLogs.CountAsync(value => value.PlayerId == player.Id && value.Reason == "Battle") == 1 && await db.CurrencyLogs.CountAsync(value => value.PlayerId == player.Id && value.Reason == "StageFirstClear") == 1, "battle and first-clear currency logs must each be written once");
Require(await db.RewardLogs.CountAsync() == 1, "reward log must be written once");
Require(await db.BattleLogs.CountAsync() == 1, "battle log must be written once");

var windowlessWalletBefore = await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == player.Id);
var windowlessSoftBefore = windowlessWalletBefore.SoftCurrency;
var windowlessPremiumBefore = windowlessWalletBefore.PremiumCurrency;
var windowlessExpBefore = player.Exp;
var windowlessLevelBefore = player.Level;
var windowlessEquipmentBefore = await db.PlayerEquipment.CountAsync(value => value.PlayerId == player.Id);
var windowlessStart = await service.StartAsync(player.Id, "stage_1_2", "windowless-start", CancellationToken.None);
var windowlessFinish = await service.FinishAsync(player.Id, windowlessStart.SessionId, "windowless-finish", false, CancellationToken.None);
var windowlessReplay = await service.FinishAsync(player.Id, windowlessStart.SessionId, "windowless-finish", true, CancellationToken.None);
Require(!windowlessFinish.Replayed && windowlessReplay.Replayed, "windowless battle finish replay state is incorrect");
Require(windowlessFinish.RewardSoftCurrency == 0 && windowlessFinish.RewardExp == 0 && string.IsNullOrEmpty(windowlessFinish.EquipmentInstanceId), "windowless normal clear granted timed battle rewards");
Require(windowlessReplay.RewardSoftCurrency == 0 && windowlessReplay.RewardExp == 0 && string.IsNullOrEmpty(windowlessReplay.EquipmentInstanceId), "windowless normal clear replay changed its original reward result");
var windowlessWalletAfter = await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == player.Id);
var stageTwoFirstClearPremium = catalog.Stages.Single(value => value.Id == "stage_1_2").FirstClearPremiumCurrency;
Require(windowlessWalletAfter.SoftCurrency == windowlessSoftBefore && windowlessWalletAfter.PremiumCurrency == windowlessPremiumBefore + stageTwoFirstClearPremium, "windowless clear changed soft currency or missed first-clear premium currency");
Require(player.Exp == windowlessExpBefore && player.Level == windowlessLevelBefore, "windowless normal clear changed player experience or level");
Require(await db.PlayerEquipment.CountAsync(value => value.PlayerId == player.Id) == windowlessEquipmentBefore, "windowless normal clear generated equipment");
Require(await db.PlayerStages.AnyAsync(value => value.PlayerId == player.Id && value.StageId == "stage_1_2" && value.Cleared), "windowless normal clear was not authoritatively persisted");
Require(await db.RewardGrants.CountAsync(value => value.PlayerId == player.Id) == 2 && await db.RewardLogs.CountAsync(value => value.PlayerId == player.Id) == 2 && await db.BattleLogs.CountAsync(value => value.PlayerId == player.Id) == 2, "windowless clear did not write exactly one grant, reward log, and battle log");
Require((await tasks.ListAsync(player.Id, CancellationToken.None)).Tasks.Single(value => value.Id == "daily_stage_3").Progress == 2, "windowless clear did not advance the stage-clear task");
var unlockedAfterWindowlessClear = await service.StartAsync(player.Id, "stage_1_3", "windowless-unlocked-stage", CancellationToken.None);
Require(unlockedAfterWindowlessClear.StageId == "stage_1_3", "windowless clear did not unlock the next stage");
db.BattleSessions.Remove(await db.BattleSessions.SingleAsync(value => value.Id == unlockedAfterWindowlessClear.SessionId));
await db.SaveChangesAsync();

var weakAccount = new Account { ExternalAccountId = "weak", Provider = "test" };
var weakPlayer = new Player { AccountId = weakAccount.Id, Nickname = "Underpowered", Level = 1, Power = 0 };
db.Accounts.Add(weakAccount); db.Players.Add(weakPlayer);
db.PlayerStages.Add(new PlayerStage { PlayerId = weakPlayer.Id, StageId = "stage_1_9", Cleared = true });
await db.SaveChangesAsync();
var weakBossStart = await service.StartAsync(weakPlayer.Id, "stage_1_10", "weak-boss-start", CancellationToken.None);
await RequireThrows<InvalidOperationException>(() => service.FinishAsync(weakPlayer.Id, weakBossStart.SessionId, "weak-boss-finish", CancellationToken.None), "underpowered player received a boss reward");
Require(!await db.RewardGrants.AnyAsync(value => value.PlayerId == weakPlayer.Id) && !await db.PlayerEquipment.AnyAsync(value => value.PlayerId == weakPlayer.Id), "rejected underpowered battle mutated rewards or inventory");
db.BattleSessions.Remove(await db.BattleSessions.SingleAsync(value => value.PlayerId == weakPlayer.Id));
db.PlayerStages.Remove(await db.PlayerStages.SingleAsync(value => value.PlayerId == weakPlayer.Id));
db.Players.Remove(weakPlayer); db.Accounts.Remove(weakAccount);
await db.SaveChangesAsync();

var wallet = await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId);
wallet.SoftCurrency = 2500;
await db.SaveChangesAsync();
var shop = new ShopService(db, currencies, clock, catalog);
var purchase = await shop.PurchaseAsync(firstLogin.PlayerId, "shop_spirit_dust", 2, "shop-key", CancellationToken.None);
var replayedPurchase = await shop.PurchaseAsync(firstLogin.PlayerId, "shop_spirit_dust", 2, "shop-key", CancellationToken.None);
Require(!purchase.Replayed && replayedPurchase.Replayed, "shop purchase must be idempotent");
Require(purchase.BalanceAfter == 500 && (await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency == 500, "shop currency debit is incorrect");
Require((await db.PlayerInventories.SingleAsync(value => value.PlayerId == firstLogin.PlayerId && value.ItemId == "item_spirit_dust")).Count == 2, "shop item grant was duplicated or missing");
await RequireThrows<InvalidOperationException>(() => shop.PurchaseAsync(firstLogin.PlayerId, "shop_spirit_dust", 4, "shop-limit", CancellationToken.None), "daily purchase limit was not enforced");
await RequireThrows<InvalidOperationException>(() => shop.PurchaseAsync(firstLogin.PlayerId, "shop_daily_afk_ticket", 1, "shop-lock", CancellationToken.None), "shop unlock realm was not enforced");

var paymentVerifier = new FixedPaymentVerifier(new ReceiptVerification(true, "platform-tx-1", "jade_60", 600, "CNY", "verified"));
var payments = new PaymentService(db, currencies, paymentVerifier, clock, catalog);
var order = await payments.CreateOrderAsync(firstLogin.PlayerId, "jade_60", CancellationToken.None);
var secondOpenOrder = await payments.CreateOrderAsync(firstLogin.PlayerId, "jade_60", CancellationToken.None);
Require(order.OrderNo != secondOpenOrder.OrderNo, "payment order numbers must be unique");
var granted = await payments.VerifyAndGrantAsync(firstLogin.PlayerId, order.OrderNo, "test-store", "signed-receipt", CancellationToken.None);
var replayedGrant = await payments.VerifyAndGrantAsync(firstLogin.PlayerId, order.OrderNo, "test-store", "signed-receipt", CancellationToken.None);
Require(granted.Status == "Granted" && replayedGrant.Status == "Granted", "verified payment was not granted idempotently");
Require((await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).PremiumCurrency == 60, "payment grant was duplicated or missing");
Require((await db.PlayerInventories.SingleAsync(value => value.PlayerId == firstLogin.PlayerId && value.ItemId == "first_charge_material")).Count == 10, "first charge material was not server granted");
Require((await db.PlayerInventories.SingleAsync(value => value.PlayerId == firstLogin.PlayerId && value.ItemId == "quick_afk_ticket")).Count == 1, "first charge quick AFK ticket was not server granted");
Require(await db.PlayerEquipment.CountAsync(value => value.PlayerId == firstLogin.PlayerId && value.BaseId == "artifact_firstlight" && value.IsLocked) == 1, "first charge equipment was not server generated and locked");
paymentVerifier.Result = new ReceiptVerification(true, "platform-tx-2", "monthly_card_30", 3000, "CNY", "monthly");
var monthlyOrder = await payments.CreateOrderAsync(firstLogin.PlayerId, "monthly_card_30", CancellationToken.None);
await payments.VerifyAndGrantAsync(firstLogin.PlayerId, monthlyOrder.OrderNo, "test-store", "monthly-receipt", CancellationToken.None);
var entitlement = await payments.GetEntitlementsAsync(firstLogin.PlayerId, CancellationToken.None);
Require(entitlement.FirstChargeClaimed && entitlement.DailyPremium == 30 && entitlement.AfkCapBonusHours == 4 && entitlement.QuickAfkBonus == 1 && entitlement.ActiveProductIds.Contains("monthly_card_30"), "monthly card entitlements were not server authoritative");
var dailyClaim = await payments.ClaimDailyEntitlementsAsync(firstLogin.PlayerId, CancellationToken.None);
var dailyReplay = await payments.ClaimDailyEntitlementsAsync(firstLogin.PlayerId, CancellationToken.None);
Require(!dailyClaim.Replayed && dailyClaim.PremiumCurrency == 30 && dailyReplay.Replayed && dailyReplay.PremiumCurrency == 0, "daily commercial entitlement claim was not idempotent");
Require((await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).PremiumCurrency == 390, "commercial premium grants are incorrect");
Require(await db.RewardLogs.CountAsync(value => value.PlayerId == firstLogin.PlayerId && value.RewardType == "Payment") == 2, "payment rewards were not written to the unified reward log exactly once");
paymentVerifier.Result = new ReceiptVerification(true, "platform-tx-3", "daily_pack_6", 600, "CNY", "daily-pack");
var dailyPackOrder = await payments.CreateOrderAsync(firstLogin.PlayerId, "daily_pack_6", CancellationToken.None);
await payments.VerifyAndGrantAsync(firstLogin.PlayerId, dailyPackOrder.OrderNo, "test-store", "daily-pack-receipt", CancellationToken.None);
Require((await db.PlayerInventories.SingleAsync(value => value.PlayerId == firstLogin.PlayerId && value.ItemId == "item_spirit_dust" && value.Category == "Consumable")).Count == 22, "paid pack item reward was not server granted");
paymentVerifier.Result = new ReceiptVerification(true, "platform-tx-4", "permanent_card_98", 9800, "CNY", "permanent");
var permanentOrder = await payments.CreateOrderAsync(firstLogin.PlayerId, "permanent_card_98", CancellationToken.None);
await payments.VerifyAndGrantAsync(firstLogin.PlayerId, permanentOrder.OrderNo, "test-store", "permanent-receipt", CancellationToken.None);
var permanentEntitlement = await payments.GetEntitlementsAsync(firstLogin.PlayerId, CancellationToken.None);
Require(permanentEntitlement.DailyPremium == 40 && permanentEntitlement.AfkCapBonusHours == 6, "permanent entitlement was not combined with the active monthly card");
paymentVerifier.Result = new ReceiptVerification(true, "platform-tx-5", "permanent_card_98", 9800, "CNY", "permanent-repeat");
var permanentRepeat = await payments.CreateOrderAsync(firstLogin.PlayerId, "permanent_card_98", CancellationToken.None);
await RequireThrows<InvalidOperationException>(() => payments.VerifyAndGrantAsync(firstLogin.PlayerId, permanentRepeat.OrderNo, "test-store", "permanent-repeat-receipt", CancellationToken.None), "permanent card lifetime limit was not enforced");
paymentVerifier.Result = new ReceiptVerification(true, "platform-tx-6", "realm_pack_core", 1800, "CNY", "locked-realm-pack");
var realmPackOrder = await payments.CreateOrderAsync(firstLogin.PlayerId, "realm_pack_core", CancellationToken.None);
await RequireThrows<InvalidOperationException>(() => payments.VerifyAndGrantAsync(firstLogin.PlayerId, realmPackOrder.OrderNo, "test-store", "realm-pack-receipt", CancellationToken.None), "locked realm pack payment was accepted");
paymentVerifier.Result = new ReceiptVerification(true, "platform-tx-1", "jade_60", 600, "CNY", "duplicate");
await RequireThrows<InvalidOperationException>(() => payments.VerifyAndGrantAsync(firstLogin.PlayerId, secondOpenOrder.OrderNo, "test-store", "another-receipt", CancellationToken.None), "provider transaction reuse was accepted");

var rankedPlayer = await db.Players.SingleAsync(value => value.Id == firstLogin.PlayerId);
rankedPlayer.Power = 5000;
rankedPlayer.RealmId = "realm_spirit_foundation";
rankedPlayer.RealmStage = 4;
player.Power = 2500;
player.RealmId = "realm_qi_coalescence";
player.RealmStage = 9;
db.PlayerStages.AddRange(
    new PlayerStage { PlayerId = firstLogin.PlayerId, StageId = "stage_1_10", Cleared = true });
await db.SaveChangesAsync();
var rankings = new RankingService(db, new MemoryRankingCache(), clock, catalog);
await rankings.RefreshAsync(RankingType.Power, null, CancellationToken.None);
await rankings.RefreshAsync(RankingType.Realm, null, CancellationToken.None);
await rankings.RefreshAsync(RankingType.Stage, null, CancellationToken.None);
var powerPage = await rankings.GetPageAsync(RankingType.Power, null, 1, 1, player.Id, CancellationToken.None);
var realmPage = await rankings.GetPageAsync(RankingType.Realm, null, 1, 20, firstLogin.PlayerId, CancellationToken.None);
var stagePage = await rankings.GetPageAsync(RankingType.Stage, null, 1, 20, null, CancellationToken.None);
Require(powerPage.Total == 2 && powerPage.Entries[0].PlayerId == firstLogin.PlayerId && powerPage.Self?.Rank == 2, "power ranking pagination or self rank is incorrect");
Require(realmPage.Entries[0].PlayerId == firstLogin.PlayerId && realmPage.Self?.Score == 304, "realm ranking score is incorrect");
Require(stagePage.Entries[0].PlayerId == firstLogin.PlayerId && stagePage.Entries[0].Score == 10010, "stage ranking score is incorrect");
Require(await db.RankingSnapshots.CountAsync() == 6, "ranking snapshots must contain one row per player and type");
await rankings.RefreshAsync(RankingType.Power, "weekly", CancellationToken.None);
var weeklyPage = await rankings.GetPageAsync(RankingType.Power, "weekly", 1, 100, null, CancellationToken.None);
Require(weeklyPage.PeriodKey.StartsWith("week:") && weeklyPage.Total == 2, "weekly ranking period is incorrect");

for (var index = 0; index < 3; index++)
{
    var taskBattle = await service.StartAsync(firstLogin.PlayerId, "stage_1_1", "task-start-" + index, CancellationToken.None);
    await service.FinishAsync(firstLogin.PlayerId, taskBattle.SessionId, "task-finish-" + index, CancellationToken.None);
}
var dailyTasks = await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None);
Require(dailyTasks.Tasks.Single(value => value.Id == "daily_stage_3").CanClaim, "authoritative stage victories did not complete daily task");
Require(dailyTasks.Tasks.Count == 6 && dailyTasks.Chests.Select(value => value.RequiredPoints).SequenceEqual(new[] { 20, 40, 60, 80, 100 }), "daily task or activity chest catalog is incomplete");
Require(dailyTasks.ActivityPoints == 40, "login and stage tasks did not award activity points");
var taskReward = await tasks.ClaimAsync(firstLogin.PlayerId, "daily_stage_3", CancellationToken.None);
var taskReplay = await tasks.ClaimAsync(firstLogin.PlayerId, "daily_stage_3", CancellationToken.None);
Require(!taskReward.Replayed && taskReplay.Replayed, "task reward claim must be idempotent");
var chestReward = await tasks.ClaimActivityChestAsync(firstLogin.PlayerId, 40, CancellationToken.None);
var chestReplay = await tasks.ClaimActivityChestAsync(firstLogin.PlayerId, 40, CancellationToken.None);
Require(!chestReward.Replayed && chestReplay.Replayed, "activity chest claim must be idempotent");

var mailEntity = new PlayerMail
{
    PlayerId = firstLogin.PlayerId, Title = "验证邮件", Body = "附件只能领取一次",
    AttachmentJson = System.Text.Json.JsonSerializer.Serialize(new RewardPayload(25, 0, new Dictionary<string, int> { ["item_mail_token"] = 2 })),
    ExpiresAtUtc = clock.UtcNow.AddDays(1)
};
var expiredMail = new PlayerMail { PlayerId = firstLogin.PlayerId, Title = "过期", ExpiresAtUtc = clock.UtcNow.AddSeconds(-1) };
db.PlayerMails.AddRange(mailEntity, expiredMail);
await db.SaveChangesAsync();
var mail = new MailService(db, rewards, clock);
Require((await mail.ListAsync(firstLogin.PlayerId, CancellationToken.None)).Count == 1, "expired mail was not filtered");
var mailReward = await mail.ClaimAsync(firstLogin.PlayerId, mailEntity.Id, CancellationToken.None);
var mailReplay = await mail.ClaimAsync(firstLogin.PlayerId, mailEntity.Id, CancellationToken.None);
Require(!mailReward.Replayed && mailReplay.Replayed, "mail attachment claim must be idempotent");
Require((await db.PlayerInventories.SingleAsync(value => value.PlayerId == firstLogin.PlayerId && value.ItemId == "item_mail_token")).Count == 2, "mail item attachment was duplicated or missing");
var activityService = new ActivityService(clock, catalog);
Require(activityService.ListActive().Single().Id == "activity_double_afk_launch" && activityService.RewardMultiplier("AfkRewardMultiplier") == 2.0, "UTC activity window or multiplier is incorrect");

var wearable = new PlayerEquipment { PlayerId = firstLogin.PlayerId, InstanceId = "equip-wear", BaseId = "weapon_cloudsteel_blade", Slot = "Weapon", Level = 5, Quality = "Rare" };
var salvage = new PlayerEquipment { PlayerId = firstLogin.PlayerId, InstanceId = "equip-salvage", BaseId = "helmet_mistveil", Slot = "Helmet", Level = 5, Quality = "Rare" };
db.PlayerEquipment.AddRange(wearable, salvage);
await db.SaveChangesAsync();
var equipmentAuthority = new EquipmentAuthorityService(db, currencies, tasks, catalog);
Require((await equipmentAuthority.EquipAsync(firstLogin.PlayerId, wearable.InstanceId, CancellationToken.None)).Slot == "Weapon", "server equipment equip failed");
var enhancement = await equipmentAuthority.EnhanceAsync(firstLogin.PlayerId, wearable.InstanceId, "enhance-key", CancellationToken.None);
var enhancementReplay = await equipmentAuthority.EnhanceAsync(firstLogin.PlayerId, wearable.InstanceId, "enhance-key", CancellationToken.None);
Require(!enhancement.Replayed && enhancementReplay.Replayed && enhancement.Level == 6 && wearable.Level == 6, "equipment enhancement was not authoritative or idempotent");
Require((await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None)).Tasks.Single(value => value.Id == "daily_enhance_1").CanClaim, "equipment enhancement did not advance the daily task");
await RequireThrows<InvalidOperationException>(() => equipmentAuthority.DecomposeAsync(firstLogin.PlayerId, wearable.InstanceId, "decompose-equipped", CancellationToken.None), "equipped item was decomposed");
var decomposition = await equipmentAuthority.DecomposeAsync(firstLogin.PlayerId, salvage.InstanceId, "decompose-key", CancellationToken.None);
var decompositionReplay = await equipmentAuthority.DecomposeAsync(firstLogin.PlayerId, salvage.InstanceId, "decompose-key", CancellationToken.None);
Require(!decomposition.Replayed && decompositionReplay.Replayed && decomposition.SoftCurrency == 200, "decomposition reward or replay is incorrect");
for (var index = 2; index <= 5; index++)
{
    var extra = new PlayerEquipment { PlayerId = firstLogin.PlayerId, InstanceId = "equip-salvage-" + index, BaseId = "helmet_mistveil", Slot = "Helmet", Level = 1, Quality = "Common" };
    db.PlayerEquipment.Add(extra);
    await db.SaveChangesAsync();
    await equipmentAuthority.DecomposeAsync(firstLogin.PlayerId, extra.InstanceId, "decompose-key-" + index, CancellationToken.None);
}
Require((await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None)).Tasks.Single(value => value.Id == "daily_decompose_5").CanClaim, "five real decompositions did not complete the daily task");

rankedPlayer.LastOfflineTimeUtc = clock.UtcNow.AddHours(-10);
await db.SaveChangesAsync();
var afk = new AfkAuthorityService(db, rewards, clock, activityService, catalog, equipmentDrops);
var equipmentBeforeAfk = await db.PlayerEquipment.CountAsync(value => value.PlayerId == firstLogin.PlayerId);
Require((await afk.PreviewAsync(firstLogin.PlayerId, CancellationToken.None)).EffectiveSeconds == 10 * 60 * 60, "AFK preview did not apply active card cap bonuses");
var afkClaim = await afk.ClaimAsync(firstLogin.PlayerId, "afk-key", CancellationToken.None);
var afkReplay = await afk.ClaimAsync(firstLogin.PlayerId, "afk-key", CancellationToken.None);
var expectedEquipmentRolls = (int)(10 * 60 / catalog.Afk.MinutesPerEquipmentRoll * activityService.RewardMultiplier("AfkRewardMultiplier"));
Require(!afkClaim.Replayed && afkReplay.Replayed && afkClaim.Reward.EquipmentRolls == expectedEquipmentRolls, "AFK claim, double activity, or replay is incorrect");
Require(await db.PlayerEquipment.CountAsync(value => value.PlayerId == firstLogin.PlayerId) == equipmentBeforeAfk + expectedEquipmentRolls, "AFK equipment rolls were not materialized exactly once in the server inventory");
var quickAfk = await afk.ClaimQuickAsync(firstLogin.PlayerId, "quick-1", CancellationToken.None);
var quickReplay = await afk.ClaimQuickAsync(firstLogin.PlayerId, "quick-1", CancellationToken.None);
var secondQuickAfk = await afk.ClaimQuickAsync(firstLogin.PlayerId, "quick-2", CancellationToken.None);
Require(!quickAfk.Replayed && quickReplay.Replayed && !secondQuickAfk.Replayed && quickAfk.Reward.EffectiveSeconds == catalog.Afk.QuickAfkHours * 60L * 60L, "Quick AFK reward or replay is incorrect");
Require(await db.PlayerEquipment.CountAsync(value => value.PlayerId == firstLogin.PlayerId) == equipmentBeforeAfk + expectedEquipmentRolls + quickAfk.Reward.EquipmentRolls + secondQuickAfk.Reward.EquipmentRolls, "Quick AFK equipment rewards were not materialized exactly once");
await RequireThrows<InvalidOperationException>(() => afk.ClaimQuickAsync(firstLogin.PlayerId, "quick-3", CancellationToken.None), "monthly Quick AFK daily allowance was not enforced");

rankedPlayer.Exp = 1000;
(await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency = 2000;
rankedPlayer.RealmId = "realm_body_tempering";
rankedPlayer.RealmStage = 1;
await db.SaveChangesAsync();
var realms = new RealmAuthorityService(db, currencies, tasks, catalog, new FixedServerRandomSource());
var realmResult = await realms.BreakthroughAsync(firstLogin.PlayerId, "realm-key", CancellationToken.None);
var realmReplay = await realms.BreakthroughAsync(firstLogin.PlayerId, "realm-key", CancellationToken.None);
Require(realmResult.Succeeded && !realmResult.Replayed && realmReplay.Replayed && realmResult.RealmStage == 2, "server realm breakthrough or replay is incorrect");

BattleFinishResult? bossFinish = null;
for (var stageNumber = 2; stageNumber <= 10; stageNumber++)
{
    var session = await service.StartAsync(firstLogin.PlayerId, "stage_1_" + stageNumber, "chapter-start-" + stageNumber, CancellationToken.None);
    bossFinish = stageNumber == 10
        ? await service.FinishAsync(firstLogin.PlayerId, session.SessionId, "chapter-finish-" + stageNumber, false, CancellationToken.None)
        : await service.FinishAsync(firstLogin.PlayerId, session.SessionId, "chapter-finish-" + stageNumber, CancellationToken.None);
}
var finalBoss = bossFinish ?? throw new InvalidOperationException("boss battle was not executed");
var bossEquipment = await db.PlayerEquipment.SingleAsync(value => value.InstanceId == finalBoss.EquipmentInstanceId);
Require(finalBoss.RewardSoftCurrency == 25 && finalBoss.RewardExp == 250, "chapter boss rewards are incorrect");
Require(new[] { "Rare", "Epic", "Legendary" }.Contains(bossEquipment.Quality), "boss did not improve equipment quality floor");
Require(await db.PlayerStages.CountAsync(value => value.PlayerId == firstLogin.PlayerId && value.Cleared) == 10, "chapter 1-1 through 1-10 was not authoritatively cleared");

rankedPlayer.RealmId = "realm_body_tempering"; rankedPlayer.RealmStage = 10; rankedPlayer.Exp = 1000;
(await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency = 2000;
await db.SaveChangesAsync();
var tribulation = await realms.BreakthroughAsync(firstLogin.PlayerId, "tribulation-key", CancellationToken.None);
var tribulationReplay = await realms.BreakthroughAsync(firstLogin.PlayerId, "tribulation-key", CancellationToken.None);
Require(tribulation.Succeeded && tribulation.RealmId == "realm_qi_coalescence" && tribulation.SpiritualRootId.StartsWith("root_"), "major realm breakthrough did not grant a spiritual root");
Require(tribulationReplay.Replayed && await db.PlayerSpiritualRoots.Where(value => value.PlayerId == firstLogin.PlayerId).SumAsync(value => value.Level) == 1, "tribulation spiritual root was duplicated on replay");
Require((await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None)).Tasks.Single(value => value.Id == "daily_tribulation_1").CanClaim, "successful major tribulation did not complete the daily task");
var rootedProfile = await new PlayerQueryService(db, catalog).GetProfileAsync(firstLogin.PlayerId, CancellationToken.None);
Require(rootedProfile.SpiritualRoots.Count == 9 && rootedProfile.SpiritualRoots.Sum(value => value.Level) == 1 && rootedProfile.SpiritualRoots.All(value => value.Level <= value.MaxLevel), "persisted spiritual roots were not returned in the authoritative profile");
var finalTaskBoard = await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None);
Require(finalTaskBoard.Tasks.Count == 6 && finalTaskBoard.Tasks.All(value => value.CanClaim || value.Claimed), "not every daily task was driven by its real authoritative gameplay event");

Console.WriteLine("PASS: full authoritative API domain, commerce, rankings, live ops, AFK, equipment, and realm idempotency verified.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task RequireThrows<T>(Func<Task> action, string message) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new InvalidOperationException(message);
}

sealed class FixedServerRandomSource : IServerRandomSource
{
    public bool Roll(double probability) => true;
    public int Next(int maxExclusive) => 0;
}

sealed class FixedClock : IServerClock
{
    public DateTime UtcNow => new(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
}

sealed class FixedPaymentVerifier(ReceiptVerification result) : IPaymentReceiptVerifier
{
    public ReceiptVerification Result { get; set; } = result;
    public Task<ReceiptVerification> VerifyAsync(string provider, string receipt, CancellationToken cancellationToken) => Task.FromResult(Result);
}
