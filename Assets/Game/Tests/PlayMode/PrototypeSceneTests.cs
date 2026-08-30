using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ImmortalLoot.UI;
using ImmortalLoot.Network;

namespace ImmortalLoot.Tests.PlayMode
{
    public sealed class PrototypeSceneTests
    {
        [UnityTest]
        public IEnumerator MainScene_AutoBattleProducesVisibleRandomLoot()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.SetPacingSpeedForTests(240f);
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            var lootObject = GameObject.Find("Loot");
            Assert.That(lootObject, Is.Not.Null, "Main scene must contain the loot display.");
            var lootText = lootObject.GetComponent<Text>();
            Assert.That(lootText, Is.Not.Null);

            var timeout = 5f;
            while (timeout > 0f && !lootText.text.Contains("最新掉落"))
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.That(lootText.text, Does.Contain("最新掉落"));
            Assert.That(lootText.text, Does.Contain("云纹青锋"));
            Assert.That(lootText.text, Does.Contain("+"));
            var before = controller.Power;
            GameObject.Find("EquipLatestButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.Power, Is.GreaterThan(before), "Equipping the visible drop must increase unified power.");
            GameObject.Find("Nav_EquipmentPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_EquipmentPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("EquipmentPageContent").GetComponent<Text>().text, Does.Contain("穿戴比较"));
            GameObject.Find("Nav_InventoryPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_InventoryPage").GetComponent<Button>().onClick.Invoke();
            var inventoryText = GameObject.Find("InventoryPageContent").GetComponent<Text>().text;
            Assert.That(inventoryText, Does.Contain("按品质降序筛选"));
            Assert.That(inventoryText, Does.Contain("批量分解"));
            Assert.That(inventoryText, Does.Contain("锁定及已穿戴装备受保护"));
            timeout = 2f;
            while (timeout > 0f && controller.StageNumber < 2) { timeout -= Time.deltaTime; yield return null; }
            Assert.That(controller.StageNumber, Is.GreaterThanOrEqualTo(2), "A victory must advance the stage chain.");

            GameObject.Find("Nav_CharacterPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_CharacterPage").GetComponent<Button>().onClick.Invoke();
            var characterText = GameObject.Find("CharacterPageContent").GetComponent<Text>().text;
            Assert.That(characterText, Does.Contain("等级"));
            Assert.That(characterText, Does.Contain("战力"));

            GameObject.Find("Nav_StagePage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_StagePage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("StagePageContent").GetComponent<Text>().text, Does.Contain("1-10 为石魇 Boss"));

            controller.ExecutePageAction("SpiritualRootPage");
            Assert.That(controller.ExecutePageAction("SpiritualRootPage"), Does.Contain("火灵根 +1"));

            GameObject.Find("Nav_RankingPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_RankingPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("RankingPageContent").GetComponent<Text>().text, Does.Contain("永久榜/周榜"));

            GameObject.Find("Nav_ActivityPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_ActivityPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("ActivityPageContent").GetComponent<Text>().text, Does.Contain("挂机收益 ×2"));

            GameObject.Find("Nav_TaskPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_TaskPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("TaskPageContent").GetComponent<Text>().text, Does.Contain("活跃度 20"));
            GameObject.Find("Action_TaskPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("TaskPageContent").GetComponent<Text>().text, Does.Contain("已领取"));

            GameObject.Find("Nav_ShopPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_ShopPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("ShopPageContent").GetComponent<Text>().text, Does.Contain("购买成功"));

            GameObject.Find("Nav_CultivationPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_CultivationPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("境界突破"));
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("九霄鸣脉录"));
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("惊霆引"));
            GameObject.Find("Action_CultivationPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("血修吸血"));
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("玄血生息章"));
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("归血小周天"));
            GameObject.Find("Action_CultivationPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("火修燃烧"));
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("烬阳归藏篇"));
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("流烬息法"));

            GameObject.Find("Nav_MailPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_MailPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("MailPageContent").GetComponent<Text>().text, Does.Contain("领取成功"));

            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("资源命令"));
            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("Mythic"));
            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("进度命令"));
            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("离线 8 小时"));
            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("清档命令"));
        }

        [UnityTest]
        public IEnumerator ServerMode_AutoBattleSynchronizesLootAndEquipRequest()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport();
            Object.FindAnyObjectByType<PrototypeGameController>().SetPacingSpeedForTests(240f);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("playmode-server", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(client);
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            var loot = GameObject.Find("Loot").GetComponent<Text>();
            var timeout = 6f;
            while (timeout > 0f && !loot.text.Contains("服务器掉落")) { timeout -= Time.deltaTime; yield return null; }
            Assert.That(loot.text, Does.Contain("[Rare] gloves_starseal"));
            Assert.That(loot.text, Does.Contain("affix_attack"));
            Assert.That(transport.Paths, Does.Contain("/battle/start"));
            Assert.That(transport.Paths, Does.Contain("/battle/finish"));
            Assert.That(transport.Paths, Does.Contain("/player/inventory"));
            GameObject.Find("EquipLatestButton").GetComponent<Button>().onClick.Invoke();
            timeout = 2f;
            while (timeout > 0f && !transport.Paths.Contains("/equipment/equip")) { timeout -= Time.deltaTime; yield return null; }
            Assert.That(transport.Paths, Does.Contain("/equipment/equip"));
            Assert.That(GameObject.Find("Profile").GetComponent<Text>().text, Does.Contain("战力 180"));
        }

        private sealed class ServerLoopTransport : IApiTransport
        {
            public readonly List<string> Paths = new List<string>();
            private static readonly string SessionId = "11111111-1111-1111-1111-111111111111";
            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                Paths.Add(request.Path);
                string json;
                switch (request.Path)
                {
                    case "/auth/login": json = "{\"playerId\":\"p1\",\"accessToken\":\"token\",\"expiresAtUtc\":\"2030-01-01T00:00:00Z\",\"isNewPlayer\":true}"; break;
                    case "/battle/start": json = "{\"sessionId\":\"" + SessionId + "\",\"stageId\":\"stage_1_1\",\"status\":\"Started\"}"; break;
                    case "/battle/finish": json = "{\"sessionId\":\"" + SessionId + "\",\"status\":\"Finished\",\"rewardSoftCurrency\":10,\"rewardExp\":25,\"equipmentInstanceId\":\"equip-online\",\"replayed\":false}"; break;
                    case "/player/inventory": json = "{\"items\":[],\"equipment\":[{\"instanceId\":\"equip-online\",\"baseId\":\"gloves_starseal\",\"slot\":\"Gloves\",\"level\":1,\"quality\":\"Rare\",\"isLocked\":false,\"isEquipped\":false,\"instanceJson\":\"{\\\"instanceId\\\":\\\"equip-online\\\",\\\"baseId\\\":\\\"gloves_starseal\\\",\\\"slot\\\":\\\"Gloves\\\",\\\"level\\\":1,\\\"quality\\\":\\\"Rare\\\",\\\"affixes\\\":[{\\\"id\\\":\\\"affix_attack\\\",\\\"value\\\":8.5}]}\"}]}"; break;
                    case "/equipment/equip": json = "{\"instanceId\":\"equip-online\",\"slot\":\"Gloves\",\"replaced\":false}"; break;
                    case "/player/profile": json = "{\"playerId\":\"p1\",\"nickname\":\"在线修士\",\"level\":1,\"exp\":25,\"realmId\":\"realm_body_tempering\",\"realmStage\":1,\"power\":180,\"softCurrency\":10,\"premiumCurrency\":0}"; break;
                    default: json = "{}"; break;
                }
                return Task.FromResult(new ApiResponse(200, json));
            }
        }
    }
}
