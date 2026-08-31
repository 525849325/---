using System;
using System.Collections;
using System.Collections.Generic;
using ImmortalLoot.Debugging;
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
        private GameObject _shopButton;
        private Button _enterButton;
        private bool _cultivationRequestInFlight;
        private string _cultivationIntentKey = string.Empty;

        private void Start()
        {
            _header = GameObject.Find("PageHeader")?.GetComponent<Text>();
            _game = FindAnyObjectByType<PrototypeGameController>();
            _login = FindAnyObjectByType<PrototypeLoginController>();
            if (_login != null) _login.ServerAuthenticated += EnterServerGameplay;
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
                if (rect.gameObject.CompareTag("Finish")) _pages[rect.name] = rect.gameObject;
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                var target = button.gameObject.name.StartsWith("Nav_", StringComparison.Ordinal) ? button.gameObject.name.Substring(4) : string.Empty;
                if (target.Length > 0) button.onClick.AddListener(() => Show(target));
                var actionTarget = button.gameObject.name.StartsWith("Action_", StringComparison.Ordinal) ? button.gameObject.name.Substring(7) : string.Empty;
                if (actionTarget.Length > 0) button.onClick.AddListener(() => Execute(actionTarget));
            }
            _shopButton = GameObject.Find("Nav_ShopPage");
            if (_shopButton != null) _shopButton.SetActive(false);
            _enterButton = GameObject.Find("EnterGameButton")?.GetComponent<Button>();
            if (_enterButton != null) _enterButton.onClick.AddListener(EnterOfflineGameplay);
            HideGameplayPages();
            if (Debug.isDebugBuild && DevelopmentPlaytestOptions.AutoQuit) StartCoroutine(EnterOfflineAfterInitialization());
        }

        private void Update()
        {
            if (_enterButton != null)
                _enterButton.interactable = _game != null && !_game.GameplayActive && (_login == null || _login.CanEnterOffline);
            if (_game == null || !_game.GameplayActive) return;
            if (_shopButton != null && !_shopButton.activeSelf && _game != null && _game.CommercialUnlocked)
            {
                _shopButton.SetActive(true);
                _game.RecordShopExposure();
            }
        }

        private async void Execute(string pageName)
        {
            if (_game == null) return;
            var content = GameObject.Find(pageName + "Content")?.GetComponent<Text>();
            if (content == null) return;
            var cultivationRequest = pageName == "CultivationPage";
            if (cultivationRequest && _cultivationRequestInFlight)
            {
                content.text = "突破请求正在由服务器确认，请勿重复点击。";
                return;
            }
            if (!_game.ServerGameplayActive)
            {
                content.text = _game.ExecutePageAction(pageName);
                return;
            }
            if (_login == null || !_login.IsServerAuthenticated || _login.ApiClient == null)
            {
                content.text = "服务器会话不可用；为保护本地进度，本次操作已取消。";
                return;
            }
            if (cultivationRequest) _cultivationRequestInFlight = true;
            content.text = "正在请求权威服务器……";
            try { content.text = await ExecuteServerAction(pageName, _login.ApiClient); }
            catch (Exception exception)
            {
                Debug.LogWarning("SERVER_UI_ACTION_FAILED: " + exception);
                content.text = "服务器操作暂未完成，请稍后重试。";
            }
            finally
            {
                if (cultivationRequest) _cultivationRequestInFlight = false;
            }
        }

        private void EnterOfflineGameplay()
        {
            if (_game == null || (_login != null && !_login.TryCommitOfflineEntry())) return;
            CompleteGameplayEntry(_game.TryEnterOfflineGameplay());
        }

        private IEnumerator EnterOfflineAfterInitialization()
        {
            yield return null;
            EnterOfflineGameplay();
        }

        private void EnterServerGameplay()
        {
            if (_game == null) return;
            try
            {
                var entered = _game.TryEnterServerGameplay();
                if (!entered) _login?.CancelPreparedServerEntry();
                CompleteGameplayEntry(entered);
            }
            catch (Exception exception)
            {
                _login?.CancelPreparedServerEntry();
                var feedback = GameObject.Find("LoginFeedback")?.GetComponent<Text>();
                if (feedback != null) feedback.text = "服务器资料加载失败，可重试或进入离线模式：" + exception.Message;
                Debug.LogError("SERVER_ENTRY_REJECTED: " + exception);
            }
        }

        private void CompleteGameplayEntry(bool entered)
        {
            if (!entered && (_game == null || !_game.GameplayActive)) return;
            var loginPage = GameObject.Find("LoginPage");
            if (loginPage != null) loginPage.SetActive(false);
            Show("BattlePage");
            if (_shopButton != null)
            {
                _shopButton.SetActive(_game.CommercialUnlocked);
                if (_shopButton.activeSelf) _game.RecordShopExposure();
            }
        }

        private void HideGameplayPages()
        {
            foreach (var pair in _pages) pair.Value.SetActive(false);
            if (_header != null) _header.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (_login != null) _login.ServerAuthenticated -= EnterServerGameplay;
        }

        private async System.Threading.Tasks.Task<string> ExecuteServerAction(string pageName, ImmortalLootApiClient api)
        {
            switch (pageName)
            {
                case "CharacterPage":
                {
                    var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(await api.GetProfileAsync());
                    var pending = profile.pendingTribulation == null
                        ? string.Empty
                        : $"\n渡劫待完成：击败下一只 Boss 晋升 {profile.pendingTribulation.targetRealmId}";
                    return $"{profile.nickname} · Lv.{profile.level}\n战力 {profile.power:N0} · 经验 {profile.exp:N0}\n修为 {profile.cultivationExperience:N0} · 破境石 {profile.breakthroughMaterial:N0}\n境界 {profile.realmId} {profile.realmStage} 阶{pending}\n灵砂 {profile.softCurrency:N0} · 仙晶 {profile.premiumCurrency:N0}";
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
                    if (string.IsNullOrEmpty(_cultivationIntentKey))
                        _cultivationIntentKey = "ui-realm-" + Guid.NewGuid().ToString("N");
                    RealmBreakthroughDto result;
                    try
                    {
                        result = ImmortalLootApiClient.Parse<RealmBreakthroughDto>(await api.BreakthroughAsync(_cultivationIntentKey));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("SERVER_BREAKTHROUGH_RESULT_UNKNOWN: " + exception);
                        try { await _game.RefreshServerProfileAsync(); }
                        catch (Exception refreshException) { Debug.LogWarning("SERVER_BREAKTHROUGH_RECONCILIATION_FAILED: " + refreshException); }
                        return "突破结果尚待确认；再次点击会复用同一安全凭据，不会重复结算。";
                    }
                    try
                    {
                        var profile = await _game.RefreshServerProfileAsync();
                        var message = FormatServerBreakthrough(result, profile);
                        if (IsKnownBreakthroughStatus(result.status)) _cultivationIntentKey = string.Empty;
                        return message;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("SERVER_BREAKTHROUGH_PROFILE_REFRESH_FAILED: " + exception);
                        return FormatServerBreakthrough(result, null) +
                               "\n服务器已确认本次突破，但资料刷新失败；再次点击会安全复用同一凭据。";
                    }
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

        private static string FormatServerBreakthrough(RealmBreakthroughDto result, PlayerProfileDto profile)
        {
            var currentLevel = profile?.level ?? 0;
            var currentExperience = profile?.cultivationExperience ?? 0;
            var currentMaterial = profile?.breakthroughMaterial ?? Math.Max(0, result.breakthroughMaterial);
            switch (result.status ?? string.Empty)
            {
                case "AdvancedStage":
                    return $"服务器境界突破至 {result.realmId} {result.realmStage} 阶\n消耗修为 {result.requiredExperience:N0} · 破境石 {result.materialSpent:N0}；剩余破境石 {currentMaterial:N0}";
                case "TribulationRequired":
                    return $"服务器渡劫已开启：目标 {result.targetRealmId}\n已预留破境石 {result.materialSpent:N0}，击败下一只 Boss 完成晋升";
                case "TrialAlreadyPending":
                    return $"服务器渡劫已在进行中：目标 {result.targetRealmId}\n击败下一只 Boss 完成晋升，本次未重复消耗资源";
                case "Failed":
                    return $"服务器突破失败，境界保持 {result.realmId} {result.realmStage} 阶\n损失破境石 {result.materialSpent:N0} · 剩余 {currentMaterial:N0}";
                case "RequirementsNotMet":
                    return $"服务器突破条件不足\n等级 {currentLevel}/{result.requiredLevel} · 修为 {currentExperience:N0}/{result.requiredExperience:N0} · 破境石 {currentMaterial:N0}/{result.requiredMaterial:N0}";
                case "MaximumRealm":
                    return "已达到服务器当前版本最高境界";
                default:
                    return "服务器返回了客户端尚未识别的突破状态；权威资料已刷新，本地未自行推断结算结果。";
            }
        }

        private static bool IsKnownBreakthroughStatus(string status)
        {
            switch (status ?? string.Empty)
            {
                case "AdvancedStage":
                case "TribulationRequired":
                case "TrialAlreadyPending":
                case "Failed":
                case "RequirementsNotMet":
                case "MaximumRealm":
                    return true;
                default:
                    return false;
            }
        }

        private static string _UnsupportedOnlineAction(string pageName) => $"{DisplayName(pageName)} 的服务器交互正在接入；当前不会修改本地模拟存档。";

        public void Show(string pageName)
        {
            foreach (var pair in _pages) pair.Value.SetActive(pair.Key == pageName);
            if (_header != null) _header.text = DisplayName(pageName);
            var content = GameObject.Find(pageName + "Content")?.GetComponent<Text>();
            if (content != null && _game != null) content.text = _game.GetPageSummary(pageName);
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
                case "TaskPage": return "成长试炼";
                case "ActivityPage": return "挂机收益";
                case "DebugPage": return "设置";
                default: return pageName;
            }
        }
    }
}
