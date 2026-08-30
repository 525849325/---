using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ImmortalLoot.Network
{
    public sealed class ApiRequest
    {
        public string Method { get; }
        public string Path { get; }
        public string JsonBody { get; }
        public string BearerToken { get; }
        public ApiRequest(string method, string path, string jsonBody, string bearerToken)
        { Method = method; Path = path; JsonBody = jsonBody ?? string.Empty; BearerToken = bearerToken ?? string.Empty; }
    }

    public sealed class ApiResponse
    {
        public long StatusCode { get; }
        public string Json { get; }
        public bool Succeeded => StatusCode >= 200 && StatusCode < 300;
        public ApiResponse(long statusCode, string json) { StatusCode = statusCode; Json = json ?? string.Empty; }
    }

    public interface IApiTransport { Task<ApiResponse> SendAsync(ApiRequest request); }

    public sealed class UnityWebRequestTransport : IApiTransport
    {
        private readonly string _baseUrl;
        public UnityWebRequestTransport(string baseUrl) => _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? throw new ArgumentException("Base URL is required.") : baseUrl.TrimEnd('/');

        public async Task<ApiResponse> SendAsync(ApiRequest request)
        {
            using (var web = new UnityWebRequest(_baseUrl + request.Path, request.Method))
            {
                web.downloadHandler = new DownloadHandlerBuffer();
                if (request.JsonBody.Length > 0)
                {
                    web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.JsonBody));
                    web.SetRequestHeader("Content-Type", "application/json");
                }
                if (request.BearerToken.Length > 0) web.SetRequestHeader("Authorization", "Bearer " + request.BearerToken);
                var operation = web.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                return new ApiResponse(web.responseCode, web.downloadHandler?.text ?? string.Empty);
            }
        }
    }

    [Serializable] public sealed class LoginBody { public string Provider; public string ExternalAccountId; public string Nickname; }
    [Serializable] public sealed class LoginDto { public string playerId; public string accessToken; public string expiresAtUtc; public bool isNewPlayer; }
    [Serializable] public sealed class BattleStartBody { public string StageId; public string IdempotencyKey; }
    [Serializable] public sealed class BattleFinishBody { public string SessionId; public string IdempotencyKey; }
    [Serializable] public sealed class BattleFinishRewardWindowBody { public string SessionId; public string IdempotencyKey; public bool RewardWindowEligible; }
    [Serializable] public sealed class InstanceBody { public string InstanceId; }
    [Serializable] public sealed class DecomposeBody { public string InstanceId; public string IdempotencyKey; }
    [Serializable] public sealed class IdempotencyBody { public string IdempotencyKey; }
    [Serializable] public sealed class ShopBuyBody { public string ProductId; public int Quantity; public string IdempotencyKey; }
    [Serializable] public sealed class PaymentOrderBody { public string ProductId; }
    [Serializable] public sealed class PaymentVerifyBody { public string OrderNo; public string Provider; public string Receipt; }
    [Serializable] public sealed class MailClaimBody { public string MailId; }
    [Serializable] public sealed class AfkRewardDto { public long effectiveSeconds; public long exp; public long softCurrency; public int materialCount; public int equipmentRolls; }
    [Serializable] public sealed class AfkClaimDto { public AfkRewardDto reward; public bool replayed; }
    [Serializable] public sealed class TaskRewardDto { public long softCurrency; public long premiumCurrency; }
    [Serializable] public sealed class TaskViewDto { public string id; public int progress; public int target; public int activityPoints; public bool canClaim; public bool claimed; public TaskRewardDto reward; }
    [Serializable] public sealed class ActivityChestDto { public int requiredPoints; public bool canClaim; public bool claimed; public TaskRewardDto reward; }
    [Serializable] public sealed class DailyTaskBoardDto { public string utcDate; public int activityPoints; public TaskViewDto[] tasks; public ActivityChestDto[] chests; }
    [Serializable] public sealed class RankingEntryDto { public int rank; public string playerId; public string nickname; public long score; }
    [Serializable] public sealed class RankingPageDto { public int type; public string periodKey; public int page; public int pageSize; public int total; public RankingEntryDto[] entries; public RankingEntryDto self; }
    [Serializable] public sealed class SpiritualRootProfileDto { public string rootId; public string name; public string element; public int level; public int maxLevel; }
    [Serializable] public sealed class PendingTribulationDto { public string targetRealmId; public long reservedMaterial; public long requiredExperience; }
    [Serializable] public sealed class PlayerProfileDto { public string playerId; public string nickname; public int level; public long exp; public long cultivationExperience; public string realmId; public int realmStage; public long breakthroughMaterial; public PendingTribulationDto pendingTribulation; public long power; public long softCurrency; public long premiumCurrency; public string currentStageId; public string[] clearedStageIds; public SpiritualRootProfileDto[] spiritualRoots; }
    [Serializable] public sealed class InventoryItemDto { public string itemId; public int count; public string category; }
    [Serializable] public sealed class EquipmentItemDto { public string instanceId; public string baseId; public string slot; public int level; public string quality; public bool isLocked; public bool isEquipped; public string instanceJson; }
    [Serializable] public sealed class InventoryDto { public InventoryItemDto[] items; public EquipmentItemDto[] equipment; }
    [Serializable] public sealed class ShopOfferDto { public string id; public string shopId; public string itemId; public int currency; public long price; public string limitType; public int limitCount; public string unlockRealmId; }
    [Serializable] public sealed class ShopPurchaseDto { public string productId; public string itemId; public int quantity; public long totalPrice; public long balanceAfter; public bool replayed; }
    [Serializable] public sealed class RealmBreakthroughDto { public string realmId; public int realmStage; public bool succeeded; public string spiritualRootId; public bool replayed; public string status; public string targetRealmId; public int requiredLevel; public long requiredExperience; public long requiredMaterial; public long materialSpent; public long breakthroughMaterial; }
    [Serializable] public sealed class MailDto { public string id; public string title; public string body; public string expiresAtUtc; public bool isRead; public bool isClaimed; }
    [Serializable] public sealed class BattleStartDto { public string sessionId; public string stageId; public string status; }
    [Serializable] public sealed class BattleFinishDto { public string sessionId; public string status; public long rewardSoftCurrency; public long rewardExp; public long rewardBreakthroughMaterial; public string equipmentInstanceId; public bool replayed; }
    [Serializable] public sealed class EquipResultDto { public string instanceId; public string slot; public bool replaced; }
    [Serializable] public sealed class EnhanceResultDto { public string instanceId; public int level; public long softCurrencyCost; public bool replayed; }
    [Serializable] public sealed class DecomposeResultDto { public string instanceId; public long softCurrency; public int essence; public bool replayed; }
    [Serializable] public sealed class ServerAffixDto { public string id; public float value; }
    [Serializable] public sealed class ServerEquipmentDto { public string instanceId; public string baseId; public string slot; public int level; public string quality; public ServerAffixDto[] affixes; }
    [Serializable] public sealed class PaymentOrderDto { public string orderNo; public string productId; public string status; public long amountMinorUnits; public string currencyCode; }
    [Serializable] public sealed class CommercialEntitlementDto { public long dailyPremium; public int afkCapBonusHours; public int quickAfkBonus; public bool firstChargeClaimed; public string[] activeProductIds; }
    [Serializable] public sealed class DailyCommercialClaimDto { public string utcDate; public long premiumCurrency; public bool replayed; }

    public sealed class ImmortalLootApiClient
    {
        private readonly IApiTransport _transport;
        public string AccessToken { get; private set; } = string.Empty;
        public ImmortalLootApiClient(IApiTransport transport) => _transport = transport ?? throw new ArgumentNullException(nameof(transport));

        public async Task<LoginDto> LoginAsync(string externalAccountId, string nickname)
        {
            var response = await Send("POST", "/auth/login", new LoginBody { Provider = "guest", ExternalAccountId = externalAccountId, Nickname = nickname }, false);
            var login = JsonUtility.FromJson<LoginDto>(Require(response));
            if (login == null || string.IsNullOrWhiteSpace(login.accessToken)) throw new InvalidOperationException("Login response did not contain an access token.");
            AccessToken = login.accessToken;
            return login;
        }

        public Task<ApiResponse> GetProfileAsync() => Send("GET", "/player/profile", null);
        public Task<ApiResponse> GetInventoryAsync() => Send("GET", "/player/inventory", null);
        public Task<ApiResponse> StartBattleAsync(string stageId, string key) => Send("POST", "/battle/start", new BattleStartBody { StageId = stageId, IdempotencyKey = key });
        public Task<ApiResponse> FinishBattleAsync(Guid sessionId, string key) => Send("POST", "/battle/finish", new BattleFinishBody { SessionId = sessionId.ToString(), IdempotencyKey = key });
        public Task<ApiResponse> FinishBattleAsync(Guid sessionId, string key, bool rewardWindowEligible) => Send("POST", "/battle/finish", new BattleFinishRewardWindowBody { SessionId = sessionId.ToString(), IdempotencyKey = key, RewardWindowEligible = rewardWindowEligible });
        public Task<ApiResponse> EquipAsync(string instanceId) => Send("POST", "/equipment/equip", new InstanceBody { InstanceId = instanceId });
        public Task<ApiResponse> DecomposeAsync(string instanceId, string key) => Send("POST", "/equipment/decompose", new DecomposeBody { InstanceId = instanceId, IdempotencyKey = key });
        public Task<ApiResponse> EnhanceAsync(string instanceId, string key) => Send("POST", "/equipment/enhance", new DecomposeBody { InstanceId = instanceId, IdempotencyKey = key });
        public Task<ApiResponse> PreviewAfkAsync() => Send("GET", "/afk/reward", null);
        public Task<ApiResponse> ClaimAfkAsync(string key) => Send("POST", "/afk/claim", new IdempotencyBody { IdempotencyKey = key });
        public Task<ApiResponse> ClaimQuickAfkAsync(string key) => Send("POST", "/afk/quick-claim", new IdempotencyBody { IdempotencyKey = key });
        public Task<ApiResponse> BreakthroughAsync(string key) => Send("POST", "/realm/breakthrough", new IdempotencyBody { IdempotencyKey = key });
        public Task<ApiResponse> GetShopAsync() => Send("GET", "/shop", null, false);
        public Task<ApiResponse> BuyAsync(string productId, int quantity, string key) => Send("POST", "/shop/buy", new ShopBuyBody { ProductId = productId, Quantity = quantity, IdempotencyKey = key });
        public Task<ApiResponse> GetRankingAsync(string type = "Power") => Send("GET", "/ranking?type=" + Uri.EscapeDataString(type), null, false);
        public Task<ApiResponse> GetTasksAsync() => Send("GET", "/tasks", null);
        public Task<ApiResponse> ClaimTaskAsync(string taskId) => Send("POST", "/tasks/" + Uri.EscapeDataString(taskId) + "/claim", null);
        public Task<ApiResponse> ClaimActivityChestAsync(int requiredPoints) => Send("POST", "/tasks/activity/" + requiredPoints + "/claim", null);
        public Task<ApiResponse> GetMailAsync() => Send("GET", "/mail", null);
        public Task<ApiResponse> ClaimMailAsync(Guid mailId) => Send("POST", "/mail/claim", new MailClaimBody { MailId = mailId.ToString() });
        public Task<ApiResponse> CreatePaymentOrderAsync(string productId) => Send("POST", "/payment/create-order", new PaymentOrderBody { ProductId = productId });
        public Task<ApiResponse> VerifyPaymentAsync(string orderNo, string provider, string receipt) => Send("POST", "/payment/verify", new PaymentVerifyBody { OrderNo = orderNo, Provider = provider, Receipt = receipt });
        public Task<ApiResponse> GetCommercialEntitlementsAsync() => Send("GET", "/payment/entitlements", null);
        public Task<ApiResponse> ClaimCommercialDailyAsync() => Send("POST", "/payment/daily-claim", null);

        private Task<ApiResponse> Send(string method, string path, object body, bool authenticated = true)
        {
            if (authenticated && AccessToken.Length == 0) throw new InvalidOperationException("Login is required.");
            var json = body == null ? string.Empty : JsonUtility.ToJson(body);
            return _transport.SendAsync(new ApiRequest(method, path, json, authenticated ? AccessToken : string.Empty));
        }

        private static string Require(ApiResponse response)
        {
            if (!response.Succeeded) throw new InvalidOperationException("Server request failed with HTTP " + response.StatusCode + ": " + response.Json);
            return response.Json;
        }

        public static T Parse<T>(ApiResponse response)
        {
            var value = JsonUtility.FromJson<T>(Require(response));
            if (value == null) throw new InvalidOperationException("Server response could not be parsed.");
            return value;
        }

        public static T[] ParseArray<T>(ApiResponse response)
        {
            var wrapper = JsonUtility.FromJson<ArrayEnvelope<T>>("{\"items\":" + Require(response) + "}");
            if (wrapper == null || wrapper.items == null) throw new InvalidOperationException("Server array response could not be parsed.");
            return wrapper.items;
        }

        [Serializable] private sealed class ArrayEnvelope<T> { public T[] items; }
    }
}
