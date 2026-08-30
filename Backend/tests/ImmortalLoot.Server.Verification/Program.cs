using ImmortalLoot.Server.Persistence;
using ImmortalLoot.Server.Services;
using ImmortalLoot.Server.Config;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connection).Options;
await using var db = new GameDbContext(options);
await db.Database.EnsureCreatedAsync();
Require(typeof(Player).GetProperty("CultivationExperience") is not null,
    "Player schema does not expose an independent cumulative cultivation experience pool");

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
Require(profile.Exp == 0 && profile.CultivationExperience == 0,
    "fresh profile did not expose independent level and cultivation experience pools");
Require(profile.CurrentStageId == "stage_1_1" && profile.ClearedStageIds.Count == 0, "fresh profile did not expose the authoritative first stage");

var account = new Account { ExternalAccountId = "verify", Provider = "test" };
var player = new Player { AccountId = account.Id, Nickname = "Verifier" };
db.Accounts.Add(account);
db.Players.Add(player);
db.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = player.Id });
db.PlayerStats.Add(new PlayerStats { PlayerId = player.Id });
await db.SaveChangesAsync();

var equipmentDrops = new ServerEquipmentDropService(db, clock, catalog);
var service = new BattleAuthorityService(db, clock, currencies, tasks, equipmentDrops, catalog);
var noncanonicalState = await CaptureBattleMutationSnapshotAsync(db, player.Id);
await RequireThrows<ArgumentException>(
    () => service.StartAsync(player.Id, "stage_1_01", "noncanonical-stage", CancellationToken.None),
    "a noncanonical stage id was accepted");
Require(await CaptureBattleMutationSnapshotAsync(db, player.Id) == noncanonicalState,
    "a rejected noncanonical stage mutated authoritative battle state");
var firstStart = await service.StartAsync(player.Id, "stage_1_1", "start-key", CancellationToken.None);
var repeatedStart = await service.StartAsync(player.Id, "stage_1_1", "start-key", CancellationToken.None);
Require(firstStart.SessionId == repeatedStart.SessionId, "battle start must be idempotent");

var firstFinish = await service.FinishAsync(player.Id, firstStart.SessionId, "finish-key", true, CancellationToken.None);
var repeatedFinish = await service.FinishAsync(player.Id, firstStart.SessionId, "finish-key", true, CancellationToken.None);
Require(!firstFinish.Replayed && repeatedFinish.Replayed, "battle finish replay state is incorrect");
Require(firstFinish.RewardExp == 0 && firstFinish.RewardSoftCurrency == 0 && string.IsNullOrEmpty(firstFinish.EquipmentInstanceId), "an untrusted client reward-window flag granted a normal-stage reward");
Require(repeatedFinish.RewardExp == 0 && string.IsNullOrEmpty(repeatedFinish.EquipmentInstanceId), "normal-stage replay changed the server-owned reward decision");
Require(await db.PlayerEquipment.CountAsync(value => value.PlayerId == player.Id) == 0, "normal-stage client flag generated equipment");
await RequireThrows<InvalidOperationException>(() => service.StartAsync(player.Id, "stage_1_3", "locked-stage", CancellationToken.None), "locked stage battle was accepted");
Require((await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == player.Id)).SoftCurrency == 0, "normal-stage client flag changed soft currency");
Require(await db.RewardGrants.CountAsync() == 1, "reward grant must be unique");
Require(await db.CurrencyLogs.CountAsync(value => value.PlayerId == player.Id && value.Reason == "Battle") == 0 && await db.CurrencyLogs.CountAsync(value => value.PlayerId == player.Id && value.Reason == "StageFirstClear") == 1, "normal-stage client flag bypassed the server reward boundary or first-clear grant was missing");
Require(await db.RewardLogs.CountAsync() == 1, "reward log must be written once");
Require(await db.BattleLogs.CountAsync() == 1, "battle log must be written once");
var profileAfterFirstClear = await new PlayerQueryService(db, catalog).GetProfileAsync(player.Id, CancellationToken.None);
Require(profileAfterFirstClear.CurrentStageId == "stage_1_2" && profileAfterFirstClear.ClearedStageIds.SequenceEqual(new[] { "stage_1_1" }), "profile did not advance its authoritative current stage after stage 1");
var stateBeforeConflictingReplay = new
{
    Battles = await db.BattleSessions.CountAsync(value => value.PlayerId == player.Id),
    Stages = await db.PlayerStages.CountAsync(value => value.PlayerId == player.Id),
    Grants = await db.RewardGrants.CountAsync(value => value.PlayerId == player.Id),
    RewardLogs = await db.RewardLogs.CountAsync(value => value.PlayerId == player.Id),
    CurrencyLogs = await db.CurrencyLogs.CountAsync(value => value.PlayerId == player.Id),
    Wallet = await db.PlayerCurrencies.AsNoTracking().SingleAsync(value => value.PlayerId == player.Id)
};
await RequireThrows<InvalidOperationException>(
    () => service.StartAsync(player.Id, "stage_1_2", "start-key", CancellationToken.None),
    "battle start idempotency key was reused for a different stage");
Require(await db.BattleSessions.CountAsync(value => value.PlayerId == player.Id) == stateBeforeConflictingReplay.Battles &&
        await db.PlayerStages.CountAsync(value => value.PlayerId == player.Id) == stateBeforeConflictingReplay.Stages &&
        await db.RewardGrants.CountAsync(value => value.PlayerId == player.Id) == stateBeforeConflictingReplay.Grants &&
        await db.RewardLogs.CountAsync(value => value.PlayerId == player.Id) == stateBeforeConflictingReplay.RewardLogs &&
        await db.CurrencyLogs.CountAsync(value => value.PlayerId == player.Id) == stateBeforeConflictingReplay.CurrencyLogs,
    "a rejected conflicting battle-start replay mutated authoritative state");
var walletAfterConflictingReplay = await db.PlayerCurrencies.AsNoTracking().SingleAsync(value => value.PlayerId == player.Id);
Require(walletAfterConflictingReplay.SoftCurrency == stateBeforeConflictingReplay.Wallet.SoftCurrency &&
        walletAfterConflictingReplay.PremiumCurrency == stateBeforeConflictingReplay.Wallet.PremiumCurrency,
    "a rejected conflicting battle-start replay mutated currency");
var replayedStartAfterProgression = await service.StartAsync(player.Id, "stage_1_1", "start-key", CancellationToken.None);
Require(replayedStartAfterProgression.SessionId == firstStart.SessionId, "battle start replay stopped being idempotent after authoritative progression advanced");

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
var profileAfterSecondClear = await new PlayerQueryService(db, catalog).GetProfileAsync(player.Id, CancellationToken.None);
Require(profileAfterSecondClear.CurrentStageId == "stage_1_3" && profileAfterSecondClear.ClearedStageIds.SequenceEqual(new[] { "stage_1_1", "stage_1_2" }), "profile did not expose the next unlocked server stage");
var unlockedAfterWindowlessClear = await service.StartAsync(player.Id, "stage_1_3", "windowless-unlocked-stage", CancellationToken.None);
Require(unlockedAfterWindowlessClear.StageId == "stage_1_3", "windowless clear did not unlock the next stage");
db.BattleSessions.Remove(await db.BattleSessions.SingleAsync(value => value.Id == unlockedAfterWindowlessClear.SessionId));
await db.SaveChangesAsync();

var weakAccount = new Account { ExternalAccountId = "weak", Provider = "test" };
var weakPlayer = new Player { AccountId = weakAccount.Id, Nickname = "Underpowered", Level = 1, Power = 0 };
db.Accounts.Add(weakAccount); db.Players.Add(weakPlayer);
db.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = weakPlayer.Id });
db.PlayerStats.Add(new PlayerStats { PlayerId = weakPlayer.Id });
db.PlayerStages.Add(new PlayerStage { PlayerId = weakPlayer.Id, StageId = "stage_1_9", Cleared = true });
await db.SaveChangesAsync();
var gappedProfile = await new PlayerQueryService(db, catalog).GetProfileAsync(weakPlayer.Id, CancellationToken.None);
Require(gappedProfile.CurrentStageId == "stage_1_1" && gappedProfile.ClearedStageIds.SequenceEqual(new[] { "stage_1_9" }), "a non-contiguous clear incorrectly unlocked a later authoritative stage");
var gappedState = await CaptureBattleMutationSnapshotAsync(db, weakPlayer.Id);
await RequireThrows<InvalidOperationException>(
    () => service.StartAsync(weakPlayer.Id, "stage_1_10", "gapped-boss-start", CancellationToken.None),
    "a non-current stage was accepted from a gapped clear history");
Require(await CaptureBattleMutationSnapshotAsync(db, weakPlayer.Id) == gappedState,
    "a rejected gapped-stage request mutated authoritative battle state");
for (var stageNumber = 1; stageNumber <= 8; stageNumber++)
    db.PlayerStages.Add(new PlayerStage { PlayerId = weakPlayer.Id, StageId = "stage_1_" + stageNumber, Cleared = true });
await db.SaveChangesAsync();
var weakBossStart = await service.StartAsync(weakPlayer.Id, "stage_1_10", "weak-boss-start", CancellationToken.None);
await RequireThrows<InvalidOperationException>(() => service.FinishAsync(weakPlayer.Id, weakBossStart.SessionId, "weak-boss-finish", CancellationToken.None), "underpowered player received a boss reward");
Require(!await db.RewardGrants.AnyAsync(value => value.PlayerId == weakPlayer.Id) && !await db.PlayerEquipment.AnyAsync(value => value.PlayerId == weakPlayer.Id), "rejected underpowered battle mutated rewards or inventory");
db.BattleSessions.Remove(await db.BattleSessions.SingleAsync(value => value.PlayerId == weakPlayer.Id));
db.PlayerStages.RemoveRange(await db.PlayerStages.Where(value => value.PlayerId == weakPlayer.Id).ToArrayAsync());
db.Players.Remove(weakPlayer); db.Accounts.Remove(weakAccount);
await db.SaveChangesAsync();
await VerifyConcurrentBattleStartIdempotency(catalog, clock);
await VerifyConcurrentDistinctBattleStartInvariant(catalog, clock);
await VerifySingleActiveBattleInvariant(catalog, clock);
await VerifyDatabaseSchemaUpgrade(clock);

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
var rankingStageMarker = new PlayerStage { PlayerId = firstLogin.PlayerId, StageId = "stage_1_10", Cleared = true };
db.PlayerStages.Add(rankingStageMarker);
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
db.PlayerStages.Remove(rankingStageMarker);
await db.SaveChangesAsync();

for (var index = 0; index < 3; index++)
{
    var taskStageId = "stage_1_" + (index + 1);
    var taskBattle = await service.StartAsync(firstLogin.PlayerId, taskStageId, "task-start-" + index, CancellationToken.None);
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
var levelBeforeAfk = rankedPlayer.Level;
var levelExperienceBeforeAfk = rankedPlayer.Exp;
var cultivationExperienceBeforeAfk = GetCultivationExperience(rankedPlayer);
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
var totalAfkExperience = checked(afkClaim.Reward.Exp + quickAfk.Reward.Exp + secondQuickAfk.Reward.Exp);
var expectedLevelProgress = ApplyLevelExperience(levelBeforeAfk, levelExperienceBeforeAfk, totalAfkExperience);
Require(rankedPlayer.Level == expectedLevelProgress.Level && rankedPlayer.Exp == expectedLevelProgress.Exp,
    "AFK and Quick AFK experience did not use the shared level progression rules");
Require(GetCultivationExperience(rankedPlayer) == cultivationExperienceBeforeAfk + totalAfkExperience,
    "AFK and Quick AFK did not grant cumulative cultivation experience exactly once");
await RequireThrows<InvalidOperationException>(() => afk.ClaimQuickAsync(firstLogin.PlayerId, "quick-3", CancellationToken.None), "monthly Quick AFK daily allowance was not enforced");

rankedPlayer.Exp = 1000;
SetCultivationExperience(rankedPlayer, 99);
(await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency = 2000;
rankedPlayer.RealmId = "realm_body_tempering";
rankedPlayer.RealmStage = 1;
await db.SaveChangesAsync();
var realms = new RealmAuthorityService(db, currencies, tasks, catalog, new FixedServerRandomSource());
var realmWalletBeforeRejection = (await db.PlayerCurrencies.AsNoTracking()
    .SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency;
var realmGrantsBeforeRejection = await db.RewardGrants.CountAsync(value => value.PlayerId == firstLogin.PlayerId);
var realmLogsBeforeRejection = await db.RewardLogs.CountAsync(value => value.PlayerId == firstLogin.PlayerId);
await RequireThrows<InvalidOperationException>(
    () => realms.BreakthroughAsync(firstLogin.PlayerId, "realm-insufficient-cultivation", CancellationToken.None),
    "realm breakthrough consumed residual level experience instead of cumulative cultivation experience");
Require(rankedPlayer.Exp == 1000 && GetCultivationExperience(rankedPlayer) == 99 && rankedPlayer.RealmStage == 1,
    "rejected realm breakthrough mutated either experience pool or realm progress");
Require((await db.PlayerCurrencies.AsNoTracking().SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency == realmWalletBeforeRejection &&
        await db.RewardGrants.CountAsync(value => value.PlayerId == firstLogin.PlayerId) == realmGrantsBeforeRejection &&
        await db.RewardLogs.CountAsync(value => value.PlayerId == firstLogin.PlayerId) == realmLogsBeforeRejection,
    "rejected realm breakthrough mutated currency or idempotency logs");
rankedPlayer.Exp = 7;
SetCultivationExperience(rankedPlayer, 1000);
await db.SaveChangesAsync();
var realmResult = await realms.BreakthroughAsync(firstLogin.PlayerId, "realm-key", CancellationToken.None);
var realmReplay = await realms.BreakthroughAsync(firstLogin.PlayerId, "realm-key", CancellationToken.None);
Require(realmResult.Succeeded && !realmResult.Replayed && realmReplay.Replayed && realmResult.RealmStage == 2, "server realm breakthrough or replay is incorrect");
Require(rankedPlayer.Exp == 7 && GetCultivationExperience(rankedPlayer) == 900,
    "realm breakthrough did not consume only cumulative cultivation experience exactly once");

BattleFinishResult? bossFinish = null;
for (var stageNumber = 4; stageNumber <= 10; stageNumber++)
{
    var session = await service.StartAsync(firstLogin.PlayerId, "stage_1_" + stageNumber, "chapter-start-" + stageNumber, CancellationToken.None);
    bossFinish = stageNumber == 10
        ? await service.FinishAsync(firstLogin.PlayerId, session.SessionId, "chapter-finish-" + stageNumber, false, CancellationToken.None)
        : await service.FinishAsync(firstLogin.PlayerId, session.SessionId, "chapter-finish-" + stageNumber, CancellationToken.None);
}
var finalBoss = bossFinish ?? throw new InvalidOperationException("boss battle was not executed");
var bossEquipment = await db.PlayerEquipment.SingleAsync(value => value.InstanceId == finalBoss.EquipmentInstanceId);
Require(finalBoss.RewardSoftCurrency == 25 && finalBoss.RewardExp == 250, "chapter boss rewards are incorrect");
Require(GetCultivationExperience(rankedPlayer) == 1150,
    "authoritative Boss reward did not add its experience to cumulative cultivation progress");
var cultivationExperienceAfterBoss = GetCultivationExperience(rankedPlayer);
var finalBossReplay = await service.FinishAsync(
    firstLogin.PlayerId, finalBoss.SessionId, "chapter-finish-10", true, CancellationToken.None);
Require(finalBossReplay.Replayed && GetCultivationExperience(rankedPlayer) == cultivationExperienceAfterBoss,
    "Boss finish replay granted cumulative cultivation experience more than once");
Require(new[] { "Rare", "Epic", "Legendary" }.Contains(bossEquipment.Quality), "boss did not improve equipment quality floor");
Require(await db.PlayerStages.CountAsync(value => value.PlayerId == firstLogin.PlayerId && value.Cleared) == 10, "chapter 1-1 through 1-10 was not authoritatively cleared");
var completedChapterProfile = await new PlayerQueryService(db, catalog).GetProfileAsync(firstLogin.PlayerId, CancellationToken.None);
Require(completedChapterProfile.CurrentStageId == "stage_1_1" && completedChapterProfile.ClearedStageIds.Count == 10, "completed chapter profile did not cycle to the authoritative first stage");
var wrappedStart = await service.StartAsync(firstLogin.PlayerId, "stage_1_1", "chapter-wrap-start", CancellationToken.None);
Require(wrappedStart.StageId == "stage_1_1", "completed chapter did not authorize the wrapped first stage");
var postWrapState = await CaptureBattleMutationSnapshotAsync(db, firstLogin.PlayerId);
await RequireThrows<InvalidOperationException>(
    () => service.StartAsync(firstLogin.PlayerId, "stage_1_2", "chapter-wrap-invalid", CancellationToken.None),
    "completed chapter authorized a non-current stage after wrapping");
Require(await CaptureBattleMutationSnapshotAsync(db, firstLogin.PlayerId) == postWrapState,
    "a rejected post-wrap stage mutated authoritative battle state");

rankedPlayer.RealmId = "realm_body_tempering"; rankedPlayer.RealmStage = 10; rankedPlayer.Exp = 13;
SetCultivationExperience(rankedPlayer, 1000);
(await db.PlayerCurrencies.SingleAsync(value => value.PlayerId == firstLogin.PlayerId)).SoftCurrency = 2000;
await db.SaveChangesAsync();
var tribulation = await realms.BreakthroughAsync(firstLogin.PlayerId, "tribulation-key", CancellationToken.None);
var tribulationReplay = await realms.BreakthroughAsync(firstLogin.PlayerId, "tribulation-key", CancellationToken.None);
Require(tribulation.Succeeded && tribulation.RealmId == "realm_qi_coalescence" && tribulation.SpiritualRootId.StartsWith("root_"), "major realm breakthrough did not grant a spiritual root");
Require(tribulationReplay.Replayed && await db.PlayerSpiritualRoots.Where(value => value.PlayerId == firstLogin.PlayerId).SumAsync(value => value.Level) == 1, "tribulation spiritual root was duplicated on replay");
Require(rankedPlayer.Exp == 13 && GetCultivationExperience(rankedPlayer) == 900,
    "major realm replay changed residual level experience or consumed cultivation experience twice");
Require((await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None)).Tasks.Single(value => value.Id == "daily_tribulation_1").CanClaim, "successful major tribulation did not complete the daily task");
var rootedProfile = await new PlayerQueryService(db, catalog).GetProfileAsync(firstLogin.PlayerId, CancellationToken.None);
Require(rootedProfile.SpiritualRoots.Count == 9 && rootedProfile.SpiritualRoots.Sum(value => value.Level) == 1 && rootedProfile.SpiritualRoots.All(value => value.Level <= value.MaxLevel), "persisted spiritual roots were not returned in the authoritative profile");
var finalTaskBoard = await tasks.ListAsync(firstLogin.PlayerId, CancellationToken.None);
Require(finalTaskBoard.Tasks.Count == 6 && finalTaskBoard.Tasks.All(value => value.CanClaim || value.Claimed), "not every daily task was driven by its real authoritative gameplay event");

Console.WriteLine("PASS: full authoritative API domain, commerce, rankings, live ops, AFK, equipment, and realm idempotency verified.");

static async Task VerifyConcurrentBattleStartIdempotency(ServerGameConfigCatalog catalog, IServerClock clock)
{
    var databasePath = Path.Combine(Path.GetTempPath(), "immortal-loot-battle-start-" + Guid.NewGuid().ToString("N") + ".db");
    var connectionString = "Data Source=" + databasePath + ";Pooling=False";
    try
    {
        var setupOptions = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connectionString).Options;
        Guid playerId;
        await using (var setup = new GameDbContext(setupOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            var account = new Account { ExternalAccountId = "concurrent-start", Provider = "test" };
            var player = new Player { AccountId = account.Id, Nickname = "Concurrent" };
            playerId = player.Id;
            setup.Accounts.Add(account);
            setup.Players.Add(player);
            setup.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = playerId });
            setup.PlayerStats.Add(new PlayerStats { PlayerId = playerId });
            await setup.SaveChangesAsync();
        }

        var barrier = new ConcurrentBattleStartSaveBarrier();
        var concurrentOptions = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(barrier)
            .Options;
        await using var firstDb = new GameDbContext(concurrentOptions);
        await using var secondDb = new GameDbContext(concurrentOptions);
        var firstService = CreateBattleService(firstDb, clock, catalog);
        var secondService = CreateBattleService(secondDb, clock, catalog);
        var starts = await Task.WhenAll(
            firstService.StartAsync(playerId, "stage_1_1", "concurrent-key", CancellationToken.None),
            secondService.StartAsync(playerId, "stage_1_1", "concurrent-key", CancellationToken.None));
        Require(starts[0].SessionId == starts[1].SessionId, "concurrent battle-start replay returned different sessions");

        await using var assertionDb = new GameDbContext(setupOptions);
        Require(await assertionDb.BattleSessions.CountAsync(value => value.PlayerId == playerId) == 1,
            "concurrent battle-start replay persisted more than one session");
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { databasePath, databasePath + "-shm", databasePath + "-wal" })
            if (File.Exists(path)) File.Delete(path);
    }
}

static async Task VerifySingleActiveBattleInvariant(ServerGameConfigCatalog catalog, IServerClock clock)
{
    var databasePath = Path.Combine(Path.GetTempPath(), "immortal-loot-single-active-" + Guid.NewGuid().ToString("N") + ".db");
    var connectionString = "Data Source=" + databasePath + ";Pooling=False";
    try
    {
        var options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connectionString).Options;
        await using var invariantDb = new GameDbContext(options);
        await invariantDb.Database.EnsureCreatedAsync();
        var account = new Account { ExternalAccountId = "single-active", Provider = "test" };
        var player = new Player
        {
            AccountId = account.Id,
            Nickname = "SingleActive",
            Power = catalog.Stages.Single(value => value.Id == "stage_1_10").RecommendedPower
        };
        invariantDb.Accounts.Add(account);
        invariantDb.Players.Add(player);
        invariantDb.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = player.Id });
        invariantDb.PlayerStats.Add(new PlayerStats { PlayerId = player.Id });
        for (var stageNumber = 1; stageNumber <= 9; stageNumber++)
            invariantDb.PlayerStages.Add(new PlayerStage
            {
                PlayerId = player.Id,
                StageId = "stage_1_" + stageNumber,
                Cleared = true,
                FirstClearTimeUtc = clock.UtcNow.AddMinutes(-stageNumber)
            });
        await invariantDb.SaveChangesAsync();

        var service = CreateBattleService(invariantDb, clock, catalog);
        var active = await service.StartAsync(player.Id, "stage_1_10", "active-key-a", CancellationToken.None);
        await using var recoveryDb = new GameDbContext(options);
        var recovered = await CreateBattleService(recoveryDb, clock, catalog)
            .StartAsync(player.Id, "stage_1_10", "active-key-after-lost-response", CancellationToken.None);
        Require(recovered.SessionId == active.SessionId,
            "a lost start response or app restart did not recover the existing active battle");
        Require(await invariantDb.BattleSessions.CountAsync(
                value => value.PlayerId == player.Id && value.Status == "Started") == 1,
            "more than one active battle session was persisted for a player");

        await service.FinishAsync(player.Id, active.SessionId, "active-finish-a", CancellationToken.None);
        var stale = new BattleSession
        {
            PlayerId = player.Id,
            StageId = "stage_1_10",
            IdempotencyKey = "legacy-stale-key",
            Status = "Started",
            StartedAtUtc = clock.UtcNow
        };
        invariantDb.BattleSessions.Add(stale);
        await invariantDb.SaveChangesAsync();
        var beforeStaleFinish = await CaptureBattleMutationSnapshotAsync(invariantDb, player.Id);
        await RequireThrows<InvalidOperationException>(
            () => service.FinishAsync(player.Id, stale.Id, "legacy-stale-finish", CancellationToken.None),
            "a stale stockpiled session granted a second stage-clear reward");
        Require(await CaptureBattleMutationSnapshotAsync(invariantDb, player.Id) == beforeStaleFinish,
            "a rejected stale session mutated rewards, logs, progression, or currency");
        Require((await invariantDb.BattleSessions.AsNoTracking().SingleAsync(value => value.Id == stale.Id)).Status == "Invalidated",
            "a rejected stale session was not invalidated");
        var nextStage = await service.StartAsync(player.Id, "stage_1_1", "active-key-next", CancellationToken.None);
        Require(nextStage.StageId == "stage_1_1", "an invalidated stale Boss session blocked the authoritative wrapped stage");
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { databasePath, databasePath + "-shm", databasePath + "-wal" })
            if (File.Exists(path)) File.Delete(path);
    }
}

static async Task VerifyConcurrentDistinctBattleStartInvariant(ServerGameConfigCatalog catalog, IServerClock clock)
{
    var databasePath = Path.Combine(Path.GetTempPath(), "immortal-loot-distinct-start-" + Guid.NewGuid().ToString("N") + ".db");
    var connectionString = "Data Source=" + databasePath + ";Pooling=False";
    try
    {
        var setupOptions = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connectionString).Options;
        Guid playerId;
        await using (var setup = new GameDbContext(setupOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            var account = new Account { ExternalAccountId = "concurrent-distinct", Provider = "test" };
            var player = new Player { AccountId = account.Id, Nickname = "DistinctConcurrent" };
            playerId = player.Id;
            setup.Accounts.Add(account);
            setup.Players.Add(player);
            setup.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = playerId });
            setup.PlayerStats.Add(new PlayerStats { PlayerId = playerId });
            await setup.SaveChangesAsync();
        }

        var barrier = new ConcurrentBattleStartSaveBarrier();
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(barrier)
            .Options;
        await using var firstDb = new GameDbContext(options);
        await using var secondDb = new GameDbContext(options);
        var firstTask = CreateBattleService(firstDb, clock, catalog)
            .StartAsync(playerId, "stage_1_1", "distinct-key-a", CancellationToken.None);
        var secondTask = CreateBattleService(secondDb, clock, catalog)
            .StartAsync(playerId, "stage_1_1", "distinct-key-b", CancellationToken.None);
        var firstOutcome = await CaptureBattleStartOutcomeAsync(firstTask);
        var secondOutcome = await CaptureBattleStartOutcomeAsync(secondTask);
        Require(firstOutcome.Error is null && secondOutcome.Error is null &&
                firstOutcome.Result is not null && secondOutcome.Result is not null &&
                firstOutcome.Result.SessionId == secondOutcome.Result.SessionId,
            "concurrent distinct-key starts did not safely recover the same active battle");

        await using var assertionDb = new GameDbContext(setupOptions);
        Require(await assertionDb.BattleSessions.CountAsync(
                value => value.PlayerId == playerId && value.Status == "Started") == 1,
            "concurrent distinct-key starts persisted more than one active session");
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { databasePath, databasePath + "-shm", databasePath + "-wal" })
            if (File.Exists(path)) File.Delete(path);
    }
}

static async Task VerifyDatabaseSchemaUpgrade(IServerClock clock)
{
    var databasePath = Path.Combine(Path.GetTempPath(), "immortal-loot-schema-upgrade-" + Guid.NewGuid().ToString("N") + ".db");
    var connectionString = "Data Source=" + databasePath + ";Pooling=False";
    try
    {
        var options = new DbContextOptionsBuilder<GameDbContext>().UseSqlite(connectionString).Options;
        await using var upgradeDb = new GameDbContext(options);
        await upgradeDb.Database.EnsureCreatedAsync();
        await upgradeDb.Database.ExecuteSqlRawAsync(
            $"DROP INDEX IF EXISTS \"{GameDatabaseInitializer.ActiveBattleIndexName}\";");
        var account = new Account { ExternalAccountId = "schema-upgrade", Provider = "test" };
        var player = new Player { AccountId = account.Id, Nickname = "SchemaUpgrade", Exp = 37 };
        var older = new BattleSession
        {
            PlayerId = player.Id,
            StageId = "stage_1_1",
            IdempotencyKey = "legacy-active-older",
            Status = "Started",
            StartedAtUtc = clock.UtcNow.AddMinutes(-1)
        };
        var newer = new BattleSession
        {
            PlayerId = player.Id,
            StageId = "stage_1_1",
            IdempotencyKey = "legacy-active-newer",
            Status = "Started",
            StartedAtUtc = clock.UtcNow
        };
        upgradeDb.Accounts.Add(account);
        upgradeDb.Players.Add(player);
        upgradeDb.PlayerCurrencies.Add(new PlayerCurrency { PlayerId = player.Id });
        upgradeDb.PlayerStats.Add(new PlayerStats { PlayerId = player.Id });
        upgradeDb.BattleSessions.AddRange(older, newer);
        await upgradeDb.SaveChangesAsync();
        await upgradeDb.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"Player\" DROP COLUMN \"CultivationExperience\";");
        upgradeDb.ChangeTracker.Clear();

        await GameDatabaseInitializer.InitializeAsync(upgradeDb, clock.UtcNow);
        var migratedPlayer = await upgradeDb.Players.AsNoTracking().SingleAsync(value => value.Id == player.Id);
        Require(migratedPlayer.Exp == 37 && GetCultivationExperience(migratedPlayer) == 37,
            "legacy Player experience was not backfilled into cumulative cultivation experience");
        await upgradeDb.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Player\" SET \"Exp\" = {91L}, \"CultivationExperience\" = {0L} WHERE \"Id\" = {player.Id};");
        await GameDatabaseInitializer.InitializeAsync(upgradeDb, clock.UtcNow);
        var secondInitialization = await upgradeDb.Players.AsNoTracking().SingleAsync(value => value.Id == player.Id);
        Require(secondInitialization.Exp == 91 && GetCultivationExperience(secondInitialization) == 0,
            "database reinitialization repeated legacy cultivation backfill and overwrote current progress");
        var upgradedSessions = await upgradeDb.BattleSessions.AsNoTracking()
            .Where(value => value.PlayerId == player.Id)
            .ToListAsync();
        Require(upgradedSessions.Single(value => value.Id == newer.Id).Status == "Started" &&
                upgradedSessions.Single(value => value.Id == older.Id).Status == "Invalidated",
            "schema upgrade did not reconcile duplicate legacy active sessions deterministically");

        var conflicting = new BattleSession
        {
            PlayerId = player.Id,
            StageId = "stage_1_1",
            IdempotencyKey = "post-upgrade-conflict",
            Status = "Started",
            StartedAtUtc = clock.UtcNow
        };
        upgradeDb.BattleSessions.Add(conflicting);
        await RequireThrows<DbUpdateException>(
            () => upgradeDb.SaveChangesAsync(),
            "schema upgrade did not install the unique active-battle index");
        upgradeDb.Entry(conflicting).State = EntityState.Detached;
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { databasePath, databasePath + "-shm", databasePath + "-wal" })
            if (File.Exists(path)) File.Delete(path);
    }
}

static async Task<BattleStartOutcome> CaptureBattleStartOutcomeAsync(Task<BattleStartResult> task)
{
    try { return new BattleStartOutcome(await task, null); }
    catch (Exception exception) { return new BattleStartOutcome(null, exception); }
}

static async Task<BattleMutationSnapshot> CaptureBattleMutationSnapshotAsync(GameDbContext db, Guid playerId)
{
    var wallet = await db.PlayerCurrencies.AsNoTracking().SingleAsync(value => value.PlayerId == playerId);
    var player = await db.Players.AsNoTracking().SingleAsync(value => value.Id == playerId);
    var taskProgress = await db.PlayerTasks.AsNoTracking()
        .Where(value => value.PlayerId == playerId)
        .Select(value => (long?)value.Progress)
        .SumAsync() ?? 0;
    return new BattleMutationSnapshot(
        await db.BattleSessions.CountAsync(value => value.PlayerId == playerId),
        await db.PlayerStages.CountAsync(value => value.PlayerId == playerId),
        await db.RewardGrants.CountAsync(value => value.PlayerId == playerId),
        await db.RewardLogs.CountAsync(value => value.PlayerId == playerId),
        await db.CurrencyLogs.CountAsync(value => value.PlayerId == playerId),
        await db.BattleLogs.CountAsync(value => value.PlayerId == playerId),
        await db.PlayerEquipment.CountAsync(value => value.PlayerId == playerId),
        await db.EquipmentLogs.CountAsync(value => value.PlayerId == playerId),
        await db.PlayerTasks.CountAsync(value => value.PlayerId == playerId && value.IsClaimed),
        taskProgress,
        player.Level,
        player.Exp,
        GetCultivationExperience(player),
        wallet.SoftCurrency,
        wallet.PremiumCurrency);
}

static BattleAuthorityService CreateBattleService(GameDbContext db, IServerClock clock, ServerGameConfigCatalog catalog)
{
    var currencies = new CurrencyService(db);
    var rewards = new RewardService(db, currencies);
    var tasks = new TaskService(db, rewards, clock, catalog);
    var drops = new ServerEquipmentDropService(db, clock, catalog);
    return new BattleAuthorityService(db, clock, currencies, tasks, drops, catalog);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static long GetCultivationExperience(Player player)
{
    var property = typeof(Player).GetProperty("CultivationExperience")
        ?? throw new InvalidOperationException("Player cultivation experience property is missing.");
    return (long)(property.GetValue(player) ?? 0L);
}

static void SetCultivationExperience(Player player, long value)
{
    var property = typeof(Player).GetProperty("CultivationExperience")
        ?? throw new InvalidOperationException("Player cultivation experience property is missing.");
    property.SetValue(player, value);
}

static (int Level, long Exp) ApplyLevelExperience(int level, long experience, long reward)
{
    experience = checked(experience + reward);
    while (experience >= level * 100L)
    {
        experience -= level * 100L;
        level++;
    }
    return (level, experience);
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

sealed record BattleStartOutcome(BattleStartResult? Result, Exception? Error);

sealed record BattleMutationSnapshot(
    int Sessions,
    int Stages,
    int Grants,
    int RewardLogs,
    int CurrencyLogs,
    int BattleLogs,
    int Equipment,
    int EquipmentLogs,
    int ClaimedTasks,
    long TaskProgress,
    int Level,
    long Exp,
    long CultivationExperience,
    long SoftCurrency,
    long PremiumCurrency);

sealed class FixedClock : IServerClock
{
    public DateTime UtcNow => new(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc);
}

sealed class FixedPaymentVerifier(ReceiptVerification result) : IPaymentReceiptVerifier
{
    public ReceiptVerification Result { get; set; } = result;
    public Task<ReceiptVerification> VerifyAsync(string provider, string receipt, CancellationToken cancellationToken) => Task.FromResult(Result);
}

sealed class ConcurrentBattleStartSaveBarrier : SaveChangesInterceptor
{
    private readonly TaskCompletionSource<bool> _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var hasPendingBattle = eventData.Context?.ChangeTracker.Entries<BattleSession>()
            .Any(entry => entry.State == EntityState.Added) == true;
        if (!hasPendingBattle) return result;
        if (Interlocked.Increment(ref _arrivals) == 2) _allArrived.TrySetResult(true);
        await _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        return result;
    }
}
