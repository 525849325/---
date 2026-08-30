using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImmortalLoot.Network;
using NUnit.Framework;

namespace ImmortalLoot.Tests
{
    public sealed class ApiClientTests
    {
        [Test]
        public async Task ApiClient_UsesBearerAndNeverSendsPlayerIdPriceOrReward()
        {
            var transport = new FakeTransport();
            var client = new ImmortalLootApiClient(transport);
            await client.LoginAsync("device-1", "云游客");
            await client.StartBattleAsync("stage_1_1", "start-key");
            await client.BuyAsync("shop_spirit_dust", 1, "buy-key");
            await client.BreakthroughAsync("realm-key");
            Assert.That(transport.Requests[1].BearerToken, Is.EqualTo("token-123"));
            foreach (var request in transport.Requests)
            {
                Assert.That(request.JsonBody, Does.Not.Contain("PlayerId"));
                Assert.That(request.JsonBody, Does.Not.Contain("Price"));
                Assert.That(request.JsonBody, Does.Not.Contain("Reward"));
            }
            Assert.That(transport.Requests[1].Path, Is.EqualTo("/battle/start"));
            Assert.That(transport.Requests[2].Path, Is.EqualTo("/shop/buy"));
        }

        [Test]
        public void AuthenticatedRequest_RequiresLogin()
        {
            var client = new ImmortalLootApiClient(new FakeTransport());
            Assert.That(() => client.GetProfileAsync(), Throws.InvalidOperationException);
        }

        [Test]
        public async Task BattleFinish_AdditiveRewardWindowFieldIsExplicitOnlyOnNewOverload()
        {
            var transport = new FakeTransport();
            var client = new ImmortalLootApiClient(transport);
            await client.LoginAsync("finish-contract", "修士");
            var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            await client.FinishBattleAsync(sessionId, "legacy-finish");
            await client.FinishBattleAsync(sessionId, "windowless-finish", false);

            Assert.That(transport.Requests[1].JsonBody, Does.Not.Contain("RewardWindowEligible"),
                "Legacy request JSON must remain wire-compatible even though the server now fails closed.");
            Assert.That(transport.Requests[2].JsonBody, Does.Contain("\"RewardWindowEligible\":false"),
                "New clients must explicitly send a false reward-window decision.");
        }

        [Test]
        public async Task LiveOpsRequests_UseAuthenticatedAuthorityRoutes_AndDtosParse()
        {
            var transport = new FakeTransport();
            var client = new ImmortalLootApiClient(transport);
            await client.LoginAsync("device-2", "修士");
            await client.GetTasksAsync();
            await client.ClaimTaskAsync("daily_login");
            await client.ClaimActivityChestAsync(20);
            await client.PreviewAfkAsync();
            await client.ClaimQuickAfkAsync("quick-1");
            await client.GetRankingAsync("Power");
            await client.GetCommercialEntitlementsAsync();
            await client.ClaimCommercialDailyAsync();
            await client.EnhanceAsync("equipment-1", "enhance-1");
            await client.DecomposeAsync("equipment-2", "decompose-1");
            Assert.That(transport.Requests[1].Path, Is.EqualTo("/tasks"));
            Assert.That(transport.Requests[2].Path, Is.EqualTo("/tasks/daily_login/claim"));
            Assert.That(transport.Requests[3].Path, Is.EqualTo("/tasks/activity/20/claim"));
            Assert.That(transport.Requests[4].BearerToken, Is.EqualTo("token-123"));
            Assert.That(transport.Requests[5].Path, Is.EqualTo("/afk/quick-claim"));
            Assert.That(transport.Requests[6].Path, Is.EqualTo("/ranking?type=Power"));
            Assert.That(transport.Requests[7].Path, Is.EqualTo("/payment/entitlements"));
            Assert.That(transport.Requests[8].Path, Is.EqualTo("/payment/daily-claim"));
            Assert.That(transport.Requests[9].Path, Is.EqualTo("/equipment/enhance"));
            Assert.That(transport.Requests[10].Path, Is.EqualTo("/equipment/decompose"));
            var parsed = ImmortalLootApiClient.Parse<AfkRewardDto>(new ApiResponse(200, "{\"effectiveSeconds\":600,\"exp\":120,\"softCurrency\":80,\"materialCount\":2,\"equipmentRolls\":2}"));
            Assert.That(parsed.effectiveSeconds, Is.EqualTo(600));
            Assert.That(parsed.equipmentRolls, Is.EqualTo(2));
            var profile = ImmortalLootApiClient.Parse<PlayerProfileDto>(new ApiResponse(200,
                "{\"playerId\":\"p1\",\"nickname\":\"修士\",\"exp\":7,\"cultivationExperience\":345,\"currentStageId\":\"stage_1_3\",\"clearedStageIds\":[\"stage_1_1\",\"stage_1_2\"],\"spiritualRoots\":[]}"));
            Assert.That(profile.exp, Is.EqualTo(7));
            Assert.That(profile.cultivationExperience, Is.EqualTo(345));
            Assert.That(profile.currentStageId, Is.EqualTo("stage_1_3"));
            Assert.That(profile.clearedStageIds, Is.EqualTo(new[] { "stage_1_1", "stage_1_2" }));
        }

        private sealed class FakeTransport : IApiTransport
        {
            public readonly List<ApiRequest> Requests = new List<ApiRequest>();
            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                Requests.Add(request);
                var body = request.Path == "/auth/login"
                    ? "{\"playerId\":\"player-1\",\"accessToken\":\"token-123\",\"expiresAtUtc\":\"2026-09-01T00:00:00Z\",\"isNewPlayer\":true}"
                    : "{}";
                return Task.FromResult(new ApiResponse(200, body));
            }
        }
    }
}
