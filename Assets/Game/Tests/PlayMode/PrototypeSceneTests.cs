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
using ImmortalLoot.Equipment;
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
            Assert.That(FindIncludingInactive("Nav_RankingPage").activeSelf, Is.False, "Feature Freeze systems must not be exposed in V0.1.");
            Assert.That(FindIncludingInactive("Nav_MailPage").activeSelf, Is.False, "Feature Freeze systems must not be exposed in V0.1.");
            Assert.That(FindIncludingInactive("Nav_ActivityPage").activeSelf, Is.False, "Feature Freeze systems must not be exposed in V0.1.");
            var scaler = Object.FindAnyObjectByType<Canvas>().GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            if (controller.AutoEquipEnabled) controller.ToggleAutoEquipSetting();
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

            GameObject.Find("Nav_TaskPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_TaskPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("TaskPageContent").GetComponent<Text>().text, Does.Contain("活跃度 20"));
            GameObject.Find("Action_TaskPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("TaskPageContent").GetComponent<Text>().text, Does.Contain("已领取"));

            GameObject.Find("Nav_ShopPage").GetComponent<Button>().onClick.Invoke();
            var currencyBeforeShop = GameObject.Find("Currencies").GetComponent<Text>().text;
            GameObject.Find("Action_ShopPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("ShopPageContent").GetComponent<Text>().text, Does.Contain("望月修行契"));
            Assert.That(GameObject.Find("Currencies").GetComponent<Text>().text, Is.EqualTo(currencyBeforeShop), "Offline product preview must never grant or debit paid currency.");

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

            GameObject.Find("Nav_DebugPage").GetComponent<Button>().onClick.Invoke();
            Assert.That(GameObject.Find("PageHeader").GetComponent<Text>().text, Is.EqualTo("设置"));
            Assert.That(controller.ExecutePageAction("DebugPage"), Does.Contain("声音："));
            Assert.That(GameObject.Find("Setting_Sound"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_Vibration"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_Save"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_Legal"), Is.Not.Null);
            Assert.That(GameObject.Find("Setting_AutoEquip"), Is.Not.Null);
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
        public IEnumerator FullProtectedInventory_RealDropSurvivesReloadAndExplicitReplacement()
        {
            DeleteLocalSave();
            var inventory = new InventoryState { EquipmentCapacity = 120 };
            for (var i = 0; i < inventory.EquipmentCapacity; i++)
            {
                inventory.Equipment.Add(new EquipmentInstance
                {
                    InstanceId = "protected-legendary-" + i,
                    BaseId = "weapon_cloudsteel_blade",
                    DisplayName = "受保护旧装备",
                    Level = 1,
                    Quality = EquipmentQuality.Fine,
                    IsLocked = true,
                    CreateTimeUtc = System.DateTime.UtcNow.AddSeconds(-i)
                });
            }
            var repository = JsonPlayerSaveRepository.CreateDefault();
            repository.Save(new PlayerSaveSnapshot
            {
                Kills = 0,
                StageElapsedSeconds = 179,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(inventory)
            });

            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.SetPacingSpeedForTests(240f);
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            var timeout = 5f;
            InventoryState afterDrop = null;
            while (timeout > 0f && afterDrop?.PendingEquipment == null)
            {
                if (repository.Exists)
                    afterDrop = JsonUtility.FromJson<InventoryState>(repository.Load().InventoryJson);
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.That(afterDrop?.PendingEquipment, Is.Not.Null,
                "A real local reward must enter the durable pending slot when the protected inventory is full.");
            var pendingId = afterDrop.PendingEquipment.InstanceId;
            Assert.That(afterDrop.PendingEquipment.Quality, Is.GreaterThanOrEqualTo(EquipmentQuality.Rare),
                "Starting immediately before the stage-10 reward window must exercise the guaranteed Rare+ boss table.");
            Assert.That(InventoryOverflowPolicy.IsHigherValue(afterDrop.PendingEquipment, afterDrop.Equipment[119]), Is.True,
                "The end-to-end replacement scenario requires the real pending drop to outrank the sacrificial old item.");
            Assert.That(controller.LatestLoot?.InstanceId, Is.EqualTo(pendingId),
                "The exact generated drop must remain visible after overflow handling.");
            Assert.That(afterDrop.Equipment, Has.Count.EqualTo(120));
            Assert.That(afterDrop.Equipment.Exists(item => item.InstanceId == pendingId), Is.False);

            timeout = 5f;
            while (timeout > 0f && controller.SkippedPendingRewardWindowsForTests == 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.That(controller.SkippedPendingRewardWindowsForTests, Is.GreaterThan(0),
                "The test must cross a later reward window and prove pending backpressure consumes it explicitly.");
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero,
                "No equipment reward backlog may remain queued while a durable pending item blocks settlement.");
            controller.SaveForTests();

            SceneManager.LoadScene("Main");
            yield return null;
            var restored = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(restored.LatestLoot?.InstanceId, Is.EqualTo(pendingId));
            Assert.That(restored.PendingRewardWindowsForTests, Is.Zero,
                "Reloading must not resurrect or silently drop a different pending-window count.");
            var reloadedInventory = JsonUtility.FromJson<InventoryState>(repository.Load().InventoryJson);
            Assert.That(reloadedInventory.PendingEquipment?.InstanceId, Is.EqualTo(pendingId));
            Assert.That(reloadedInventory.Equipment.Count, Is.EqualTo(120));
            Assert.That(reloadedInventory.Equipment.Exists(item => item.InstanceId == pendingId), Is.False,
                "The pending item must remain outside the full regular inventory until space is available.");

            var firstAction = restored.ExecutePageAction("InventoryPage");
            Assert.That(firstAction, Does.Contain("再次执行"),
                "Destroying a protected item must require an explicit second action.");
            var afterWarning = JsonUtility.FromJson<InventoryState>(repository.Load().InventoryJson);
            Assert.That(afterWarning.PendingEquipment?.InstanceId, Is.EqualTo(pendingId));
            Assert.That(afterWarning.Equipment, Has.Count.EqualTo(120));

            var replacement = restored.ExecutePageAction("InventoryPage");
            Assert.That(replacement, Does.Contain("已牺牲"));
            var afterReplacement = JsonUtility.FromJson<InventoryState>(repository.Load().InventoryJson);
            Assert.That(afterReplacement.PendingEquipment, Is.Null);
            Assert.That(afterReplacement.Equipment, Has.Count.EqualTo(120));
            Assert.That(afterReplacement.Equipment.FindAll(item => item.InstanceId == pendingId), Has.Count.EqualTo(1));

            restored.EquipLatest();
            yield return null;
            restored.SaveForTests();
            Assert.That(repository.Load().EquippedInstanceIdsJson, Does.Contain(pendingId),
                "The recovered pending item must be genuinely equippable and persisted as equipped.");
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
            var firstClaimCurrency = GameObject.Find("Currencies").GetComponent<Text>().text;
            Assert.That(firstClaimCurrency, Does.Not.Contain("灵砂 100 "));
            Object.FindAnyObjectByType<PrototypeGameController>().SaveForTests();

            SceneManager.LoadScene("Main");
            yield return null;
            Assert.That(GameObject.Find("Currencies").GetComponent<Text>().text, Is.EqualTo(firstClaimCurrency));
            DeleteLocalSave();
        }

        private static void DeleteLocalSave()
        {
            if (File.Exists(JsonPlayerSaveRepository.DefaultPath)) File.Delete(JsonPlayerSaveRepository.DefaultPath);
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
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
