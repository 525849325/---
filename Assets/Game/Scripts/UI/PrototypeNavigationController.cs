using System;
using System.Collections.Generic;
using ImmortalLoot.Network;
using ImmortalLoot.Payment;
using UnityEngine;
using UnityEngine.UI;

namespace ImmortalLoot.UI
{
    public sealed class PrototypeNavigationController : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> _pages = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private Text _header;
        private PrototypeGameController _game;
        private PrototypeLoginController _login;

        private void Start()
        {
            _header = GameObject.Find("PageHeader")?.GetComponent<Text>();
            _game = FindAnyObjectByType<PrototypeGameController>();
            _login = FindAnyObjectByType<PrototypeLoginController>();
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
                if (rect.gameObject.CompareTag("Finish")) _pages[rect.name] = rect.gameObject;
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                var target = button.gameObject.name.StartsWith("Nav_", StringComparison.Ordinal) ? button.gameObject.name.Substring(4) : string.Empty;
                if (target.Length > 0) button.onClick.AddListener(() => Show(target));
                var actionTarget = button.gameObject.name.StartsWith("Action_", StringComparison.Ordinal) ? button.gameObject.name.Substring(7) : string.Empty;
                if (actionTarget.Length > 0) button.onClick.AddListener(() => Execute(actionTarget));
            }
            var enter = GameObject.Find("EnterGameButton")?.GetComponent<Button>();
            if (enter != null) enter.onClick.AddListener(() => { GameObject.Find("LoginPage")?.SetActive(false); Show("BattlePage"); });
            Show("BattlePage");
        }

        private async void Execute(string pageName)
        {
            if (_game == null) return;
            var content = GameObject.Find(pageName + "Content")?.GetComponent<Text>();
            if (content == null) return;
            if (_login == null || !_login.IsServerAuthenticated)
            {
                content.text = _game.ExecutePageAction(pageName);
                return;
            }
            content.text = "正在请求权威服务器……";
            try { content.text = await ExecuteServerAction(pageName, _login.ApiClient); }
            catch (Exception exception) { content.text = "服务器操作失败：" + exception.Message; }
        }

        private static async System.Threading.Tasks.Task<string> ExecuteServerAction(string pageName, ImmortalLootApiClient api)
        {
            switch (pageName)
            {
                case "CharacterPage":
                {
                    var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(await api.GetProfileAsync());
                    return $"{profile.nickname} · Lv.{profile.level}\n战力 {profile.power:N0} · 经验 {profile.exp:N0}\n境界 {profile.realmId} {profile.realmStage} 阶\n灵砂 {profile.softCurrency:N0} · 仙晶 {profile.premiumCurrency:N0}";
                }
                case "InventoryPage":
                {
                    var inventory = ImmortalLootApiClient.Parse<InventoryDto>(await api.GetInventoryAsync());
                    var text = $"服务器背包：道具 {(inventory.items == null ? 0 : inventory.items.Length)} 类 · 装备 {(inventory.equipment == null ? 0 : inventory.equipment.Length)} 件\n";
                    EquipmentItemDto salvage = null;
                    if (inventory.equipment != null) foreach (var item in inventory.equipment)
                    {
                        text += $"[{item.quality}] {item.baseId} Lv.{item.level}{(item.isEquipped ? " · 已穿戴" : string.Empty)}{(item.isLocked ? " · 已锁定" : string.Empty)}\n";
                        if (salvage == null && !item.isEquipped && !item.isLocked) salvage = item;
                    }
                    if (salvage != null)
                    {
                        var result = ImmortalLootApiClient.Parse<DecomposeResultDto>(await api.DecomposeAsync(salvage.instanceId, "ui-decompose-" + Guid.NewGuid().ToString("N")));
                        text += $"\n已安全分解 {salvage.baseId}：灵砂 +{result.softCurrency:N0} · 精华 +{result.essence}\n累计 5 次会完成每日分解任务。";
                    }
                    else text += "\n没有可分解的未锁定、未穿戴装备。";
                    return text.TrimEnd();
                }
                case "EquipmentPage":
                {
                    var inventory = ImmortalLootApiClient.Parse<InventoryDto>(await api.GetInventoryAsync());
                    if (inventory.equipment == null || inventory.equipment.Length == 0) return "服务器背包暂无装备，请先完成战斗或领取挂机收益。";
                    var target = inventory.equipment[0];
                    var enhanced = ImmortalLootApiClient.Parse<EnhanceResultDto>(await api.EnhanceAsync(target.instanceId, "ui-enhance-" + Guid.NewGuid().ToString("N")));
                    return $"服务器强化成功：{target.baseId}\nLv.{target.level} → Lv.{enhanced.level} · 灵砂 -{enhanced.softCurrencyCost:N0}\n强化费用来自共享配置，并已推进每日强化任务。";
                }
                case "ShopPage":
                {
                    var offers = ImmortalLootApiClient.ParseArray<ShopOfferDto>(await api.GetShopAsync());
                    if (offers.Length == 0) return "服务器商城当前无商品。";
                    var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(await api.GetProfileAsync());
                    ShopOfferDto offer = null;
                    foreach (var candidate in offers)
                    {
                        var unlocked = string.IsNullOrEmpty(candidate.unlockRealmId) || candidate.unlockRealmId == profile.realmId;
                        var balance = candidate.currency == 0 ? profile.softCurrency : profile.premiumCurrency;
                        if (unlocked && balance >= candidate.price) { offer = candidate; break; }
                    }
                    if (offer == null)
                    {
                        var order = ImmortalLootApiClient.Parse<PaymentOrderDto>(await api.CreatePaymentOrderAsync("jade_60"));
                        var platform = await new MockPaymentProvider().PurchaseAsync(new PaymentRequest(order.orderNo, order.productId));
                        if (!platform.Succeeded) return "Mock 支付取消：" + platform.Error;
                        var granted = ImmortalLootApiClient.Parse<PaymentOrderDto>(await api.VerifyPaymentAsync(order.orderNo, platform.Provider, platform.Receipt));
                        var refreshed = ImmortalLootApiClient.Parse<PlayerProfileDto>(await api.GetProfileAsync());
                        var entitlement = ImmortalLootApiClient.Parse<CommercialEntitlementDto>(await api.GetCommercialEntitlementsAsync());
                        return $"Mock 支付链路完成：{granted.productId}\n订单 {granted.status} · 仙晶余额 {refreshed.premiumCurrency:N0}\n首充权益 {(entitlement.firstChargeClaimed ? "已激活" : "未激活")} · 日领仙晶 {entitlement.dailyPremium:N0}\n回执和权益均由 Development 服务器验证，客户端不直接发币。";
                    }
                    try
                    {
                        var purchase = ImmortalLootApiClient.Parse<ShopPurchaseDto>(await api.BuyAsync(offer.id, 1, "ui-shop-" + Guid.NewGuid().ToString("N")));
                        return $"服务器购买成功：{purchase.itemId} ×{purchase.quantity}\n消耗 {purchase.totalPrice:N0} · 余额 {purchase.balanceAfter:N0}";
                    }
                    catch (Exception exception) { return $"商品：{offer.itemId} · 价格 {offer.price:N0}\n购买未完成：{exception.Message}"; }
                }
                case "CultivationPage":
                {
                    var result = ImmortalLootApiClient.Parse<RealmBreakthroughDto>(await api.BreakthroughAsync("ui-realm-" + Guid.NewGuid().ToString("N")));
                    return result.succeeded
                        ? $"服务器突破成功：{result.realmId} {result.realmStage} 阶\n灵根：{(string.IsNullOrEmpty(result.spiritualRootId) ? "尚未觉醒" : result.spiritualRootId)}"
                        : $"本次突破失败，境界保持 {result.realmId} {result.realmStage} 阶";
                }
                case "SpiritualRootPage":
                {
                    var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(await api.GetProfileAsync());
                    var text = "服务器九系灵根\n";
                    if (profile.spiritualRoots != null) foreach (var root in profile.spiritualRoots) text += $"{root.name} Lv.{root.level}/{root.maxLevel}  ";
                    return text + "\n大境界渡劫成功后随机成长，结果持久化且不可重放。";
                }
                case "MailPage":
                {
                    var mails = ImmortalLootApiClient.ParseArray<MailDto>(await api.GetMailAsync());
                    MailDto pending = null;
                    foreach (var mail in mails) if (!mail.isClaimed) { pending = mail; break; }
                    if (pending == null) return mails.Length == 0 ? "服务器暂无有效飞简。" : "所有有效飞简附件均已领取。";
                    await api.ClaimMailAsync(Guid.Parse(pending.id));
                    return $"已领取服务器飞简：{pending.title}\n附件由奖励流水幂等发放。";
                }
                case "ActivityPage":
                {
                    var preview = ImmortalLootApiClient.Parse<AfkRewardDto>(await api.PreviewAfkAsync());
                    var claim = ImmortalLootApiClient.Parse<AfkClaimDto>(await api.ClaimAfkAsync("ui-afk-" + Guid.NewGuid().ToString("N")));
                    var quick = ImmortalLootApiClient.Parse<AfkClaimDto>(await api.ClaimQuickAfkAsync("ui-quick-afk-" + Guid.NewGuid().ToString("N")));
                    return $"挂机收益已由服务器结算\n离线 {preview.effectiveSeconds / 60} 分钟 · 经验 +{claim.reward.exp:N0}\n灵砂 +{claim.reward.softCurrency:N0} · 材料 +{claim.reward.materialCount} · 装备 {claim.reward.equipmentRolls} 次\n快速挂机 {quick.reward.effectiveSeconds / 3600} 小时已领取；每日次数含卡权益并由服务器限制";
                }
                case "TaskPage":
                {
                    var board = ImmortalLootApiClient.Parse<DailyTaskBoardDto>(await api.GetTasksAsync());
                    TaskViewDto task = null;
                    if (board.tasks != null) foreach (var candidate in board.tasks) if (candidate.canClaim) { task = candidate; break; }
                    if (task != null) await api.ClaimTaskAsync(task.id);
                    else
                    {
                        ActivityChestDto chest = null;
                        if (board.chests != null) foreach (var candidate in board.chests) if (candidate.canClaim) { chest = candidate; break; }
                        if (chest != null) await api.ClaimActivityChestAsync(chest.requiredPoints);
                    }
                    var refreshed = ImmortalLootApiClient.Parse<DailyTaskBoardDto>(await api.GetTasksAsync());
                    var claimed = 0;
                    if (refreshed.tasks != null) foreach (var value in refreshed.tasks) if (value.claimed) claimed++;
                    return $"服务器每日任务 · {refreshed.utcDate}\n活跃度 {refreshed.activityPoints} · 已领取任务 {claimed}/{(refreshed.tasks == null ? 0 : refreshed.tasks.Length)}\n再次点击可领取下一项任务或活跃宝箱";
                }
                case "RankingPage":
                {
                    var page = ImmortalLootApiClient.Parse<RankingPageDto>(await api.GetRankingAsync("Power"));
                    var text = $"服务器战力榜 · {page.periodKey}\n";
                    if (page.entries != null) foreach (var entry in page.entries) text += $"#{entry.rank} {entry.nickname}  {entry.score:N0}\n";
                    if (page.self != null) text += $"我的排名 #{page.self.rank} · {page.self.score:N0}";
                    return text.TrimEnd();
                }
                default: return _UnsupportedOnlineAction(pageName);
            }
        }

        private static string _UnsupportedOnlineAction(string pageName) => $"{DisplayName(pageName)} 的服务器交互正在接入；当前不会修改本地模拟存档。";

        public void Show(string pageName)
        {
            foreach (var pair in _pages) pair.Value.SetActive(pair.Key == pageName);
            if (_header != null) _header.text = DisplayName(pageName);
        }

        private static string DisplayName(string pageName)
        {
            switch (pageName)
            {
                case "BattlePage": return "青崖历练";
                case "CharacterPage": return "角色 / 战力";
                case "EquipmentPage": return "装备 / 词条比较";
                case "InventoryPage": return "背包 / 分解";
                case "CultivationPage": return "修炼 / 境界 / 功法";
                case "SpiritualRootPage": return "九系灵根";
                case "StagePage": return "第一章 · 青崖遗境";
                case "ShopPage": return "云游商铺";
                case "RankingPage": return "三榜争锋";
                case "MailPage": return "飞简邮件";
                case "TaskPage": return "每日修行";
                case "ActivityPage": return "限时活动";
                case "DebugPage": return "设置";
                default: return pageName;
            }
        }
    }
}
