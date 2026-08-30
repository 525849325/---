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
using ImmortalLoot.Player;
using ImmortalLoot.Inventory;
using System.IO;

namespace ImmortalLoot.Tests.PlayMode
{
    public sealed class PrototypeSceneTests
    {
        [UnityTest]
        public IEnumerator MainScene_AutoBattleProducesVisibleRandomLoot()
        {
            DeleteLocalSave();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            var feedback = Object.FindAnyObjectByType<PrototypeCombatFeedback>();
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.GeneratedClipCount, Is.EqualTo(5), "Combat, critical, Boss, loot and equip cues must be available without external assets.");
            Assert.That(GameObject.Find("LoginTitle").GetComponent<Text>().text, Does.StartWith("《太初：无尽轮回》"));
            Assert.That(FindIncludingInactive("Nav_ShopPage").activeSelf, Is.False, "Commercial entry must stay hidden until the player understands equipment growth.");
            var scaler = Object.FindAnyObjectByType<Canvas>().GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
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
            Assert.That(lootText.text, Does.Contain(controller.LatestLoot.DisplayName));
            Assert.That(lootText.text, Does.Contain("+"));
            Assert.That(lootText.color, Is.EqualTo(PrototypeVisualTheme.QualityColor(controller.LatestLoot.Quality)));
            Assert.That(FindIncludingInactive("Nav_ShopPage").activeSelf, Is.True, "First equipment should unlock the optional shop entry without a popup.");
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
            var currencyBeforeShop = GameObject.Find("Currency").GetComponent<Text>().text;
            GameObject.Find("Action_ShopPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("ShopPageContent").GetComponent<Text>().text, Does.Contain("望月修行契"));
            Assert.That(GameObject.Find("Currency").GetComponent<Text>().text, Is.EqualTo(currencyBeforeShop), "Offline product preview must never grant or debit paid currency.");

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

            GameObject.Find("Nav_DebugPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("PageHeader").GetComponent<Text>().text, Is.EqualTo("设置"));
            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("声音："));
            Assert.That(GameObject.Find("Setting_Sound"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_Vibration"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_Save"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_Legal"), Is.Not.Null);
            GameObject.Find("Setting_Save").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("DebugPageContent").GetComponent<Text>().text, Does.Contain("进度已安全保存"));
            GameObject.Find("Setting_Legal").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("DebugPageContent").GetComponent<Text>().text, Does.Contain("隐私政策与用户协议"));
        }

        [UnityTest]
        public IEnumerator ServerMode_AutoBattleSynchronizesLootAndEquipRequest()
        {
            DeleteLocalSave();
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

        [UnityTest]
        public IEnumerator LocalSave_ReloadRestoresEquippedPowerAndStage()
        {
            DeleteLocalSave();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.SetPacingSpeedForTests(240f);
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            var timeout = 5f;
            while (timeout > 0f && controller.LatestLoot == null) { timeout -= Time.deltaTime; yield return null; }
            Assert.That(controller.LatestLoot, Is.Not.Null);
            controller.EquipLatest();
            yield return null;
            controller.SaveForTests();
            var savedPower = controller.Power;
            var savedStage = controller.StageNumber;

            SceneManager.LoadScene("Main");
            yield return null;
            var restored = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(restored.Power, Is.EqualTo(savedPower));
            Assert.That(restored.StageNumber, Is.EqualTo(savedStage));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator OfflineProgress_IsCappedAndCannotBeClaimedTwice()
        {
            DeleteLocalSave();
            var repository = JsonPlayerSaveRepository.CreateDefault();
            repository.Save(new PlayerSaveSnapshot
            {
                SoftCurrency = 100,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(new InventoryState { EquipmentCapacity = 120 })
            });
            SceneManager.LoadScene("Main");
            yield return null;
            var firstClaimCurrency = GameObject.Find("Currency").GetComponent<Text>().text;
            Assert.That(firstClaimCurrency, Does.Not.Contain("灵砂 100 "));
            Object.FindAnyObjectByType<PrototypeGameController>().SaveForTests();

            SceneManager.LoadScene("Main");
            yield return null;
            Assert.That(GameObject.Find("Currency").GetComponent<Text>().text, Is.EqualTo(firstClaimCurrency));
            DeleteLocalSave();
        }

        private static void DeleteLocalSave()
        {
            if (File.Exists(JsonPlayerSaveRepository.DefaultPath)) File.Delete(JsonPlayerSaveRepository.DefaultPath);
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (transform.name == name) return transform.gameObject;
            return null;
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
