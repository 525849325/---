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
using ImmortalLoot.Battle;
using ImmortalLoot.Cultivation;
using ImmortalLoot.Realm;
using ImmortalLoot.SpiritualRoot;
using ImmortalLoot.Analytics;
using System.IO;

namespace ImmortalLoot.Tests.PlayMode
{
    public sealed class PrototypeSceneTests
    {
        private string _saveDirectory;
        private System.IDisposable _saveOverride;
        private RecordingValidationSink _validationSink;
        private System.IDisposable _validationOverride;

        [OneTimeSetUp]
        public void UseTemporarySaveRepository()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "immortal-loot-playmode-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDirectory);
            _saveOverride = JsonPlayerSaveRepository.OverrideDefaultPathForTests(Path.Combine(_saveDirectory, "save.json"));
            _validationSink = new RecordingValidationSink();
            _validationOverride = PrototypeGameController.OverrideValidationSinkForTests(_validationSink);
        }

        [OneTimeTearDown]
        public void ReleaseTemporarySaveRepository()
        {
            foreach (var controller in Object.FindObjectsByType<PrototypeGameController>(FindObjectsInactive.Include))
                Object.DestroyImmediate(controller.gameObject);
            _validationOverride?.Dispose();
            _saveOverride?.Dispose();
            if (!string.IsNullOrEmpty(_saveDirectory) && Directory.Exists(_saveDirectory))
                Directory.Delete(_saveDirectory, recursive: true);
        }

        [UnityTest]
        public IEnumerator LoginPage_BlocksGameplayUntilOfflineEntry()
        {
            DeleteLocalSave();
            _validationSink.Events.Clear();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.SetPacingSpeedForTests(240f);
            var pacingBeforeWait = controller.PacingElapsedSecondsForTests;

            var wait = 0.35f;
            while (wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(controller.PacingElapsedSecondsForTests, Is.EqualTo(pacingBeforeWait).Within(0.01d),
                "Pacing must not advance behind the login page.");
            Assert.That(controller.LatestLoot, Is.Null, "Hidden gameplay must not generate loot before entry.");
            Assert.That(controller.GameplayActive, Is.False);
            Assert.That(controller.HasActiveBattleForTests, Is.False, "No encounter may be created behind the login page.");
            Assert.That(controller.SaveOperationCountForTests, Is.Zero, "Waiting at login must not checkpoint or consume offline time.");
            Assert.That(_validationSink.Events, Is.Empty, "Gameplay funnel events must wait for actual entry.");
            Assert.That(FindIncludingInactive("LoginPage").activeSelf, Is.True);
            Assert.That(FindIncludingInactive("BattlePage").activeSelf, Is.False);

            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.GameplayActive, Is.True);
            Assert.That(controller.ServerGameplayActive, Is.False);
            Assert.That(controller.HasActiveBattleForTests, Is.True);
            Assert.That(FindIncludingInactive("LoginPage").activeSelf, Is.False);
            Assert.That(FindIncludingInactive("BattlePage").activeSelf, Is.True);
            Assert.That(_validationSink.Events.ConvertAll(value => value.eventName),
                Is.EqualTo(new[] { "session_started", "battle_visible" }));
            Assert.That(controller.TryEnterOfflineGameplay(), Is.False, "Repeated entry must be idempotent.");
            Assert.That(_validationSink.Events, Has.Count.EqualTo(2));
            var timeout = 2f;
            while (timeout > 0f && controller.PacingElapsedSecondsForTests <= pacingBeforeWait)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(controller.PacingElapsedSecondsForTests, Is.GreaterThan(pacingBeforeWait),
                "The same production entry button must activate gameplay.");

            var lateClient = new ImmortalLootApiClient(new ServerLoopTransport());
            var lateLogin = lateClient.LoginAsync("late-server-after-offline", "迟到认证");
            while (!lateLogin.IsCompleted) yield return null;
            Assert.That(lateLogin.Exception, Is.Null);
            var login = Object.FindAnyObjectByType<PrototypeLoginController>();
            Assert.Throws<System.InvalidOperationException>(() => login.UseAuthenticatedClientForTests(
                lateClient, CreateServerProfile(), CreateServerInventory()));
            Assert.That(controller.ServerGameplayActive, Is.False,
                "A completed offline session must never switch authority mode because of a late token.");
        }

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
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_1"),
                "Actual victories, rather than elapsed time alone, must write stage-clear progress.");

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
            Assert.That(GameObject.Find("CultivationPageContent").GetComponent<Text>().text, Does.Contain("突破条件不足"));
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
        public IEnumerator CorruptOfflineSave_IsNotReadOrQuarantinedBeforeSuccessfulServerEntry()
        {
            DeleteLocalSave();
            var corruptBytes = System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":3,\"payload\":\"tampered\",\"checksum\":\"invalid\"}");
            File.WriteAllBytes(JsonPlayerSaveRepository.DefaultPath, corruptBytes);
            var saveDirectory = Path.GetDirectoryName(JsonPlayerSaveRepository.DefaultPath);
            var saveName = Path.GetFileName(JsonPlayerSaveRepository.DefaultPath);
            var quarantineBeforeEntry = Directory.GetFiles(saveDirectory, saveName + ".corrupt-*");

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();

            Assert.That(controller.GameplayActive, Is.False);
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(corruptBytes),
                "Opening the login page must not read, rewrite or quarantine a corrupt offline save.");
            Assert.That(Directory.GetFiles(saveDirectory, saveName + ".corrupt-*"), Is.EquivalentTo(quarantineBeforeEntry));

            var client = new ImmortalLootApiClient(new ServerLoopTransport());
            var loginTask = client.LoginAsync("server-entry-with-corrupt-offline-save", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());

            Assert.That(controller.GameplayActive, Is.True);
            Assert.That(controller.ServerGameplayActive, Is.True);
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(corruptBytes),
                "A successful server entry must not touch the deferred corrupt offline save.");
            Assert.That(Directory.GetFiles(saveDirectory, saveName + ".corrupt-*"), Is.EquivalentTo(quarantineBeforeEntry),
                "Only an explicit offline entry may load and quarantine a corrupt offline save.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerMode_NeverMutatesLocalSave()
        {
            DeleteLocalSave();
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                SoftCurrency = 100,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(new InventoryState { EquipmentCapacity = 120 })
            });
            var saveBeforeServerEntry = File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath);
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller.GameplayActive, Is.False);
            Assert.That(controller.SoftCurrencyForTests, Is.Zero,
                "The login screen must not load the offline snapshot before the player chooses offline mode.");

            var transport = new ServerLoopTransport();
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("server-entry-no-local-afk", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());

            Assert.That(controller.GameplayActive, Is.True);
            Assert.That(controller.ServerGameplayActive, Is.True);
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeServerEntry),
                "Server entry must leave the complete offline save byte-for-byte unchanged.");

            controller.PauseApplicationForTests();
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeServerEntry),
                "Pausing a server session must not consume the local offline window.");
            controller.SaveForTests();
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeServerEntry),
                "An explicit checkpoint in server mode must not overwrite the offline save.");

            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRequestBodies.Count < 1) { timeout -= Time.deltaTime; yield return null; }
            Assert.That(transport.FinishRequestBodies, Has.Count.EqualTo(1));
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeServerEntry),
                "A confirmed authoritative settlement must not mirror into the offline save.");
            Assert.That(controller.SaveOperationCountForTests, Is.Zero,
                "No local repository write is allowed during a server-authoritative session.");
            Assert.That(controller.TryEnterOfflineGameplay(), Is.False);
            Assert.That(controller.ServerGameplayActive, Is.True, "Authority mode must remain fixed for the session.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator FreshServerAccount_IgnoresAdvancedLocalStagePacingInventoryAndCurrency()
        {
            DeleteLocalSave();
            var localProgress = new PlayerProgressState { CurrentStageId = "stage_1_7" };
            for (var stage = 1; stage <= 6; stage++) localProgress.Stage.ClearedStageIds.Add("stage_1_" + stage);
            var localInventory = new InventoryState { EquipmentCapacity = 120 };
            localInventory.Equipment.Add(new EquipmentInstance
            {
                InstanceId = "offline-only-equipment",
                BaseId = "weapon_cloudsteel_blade",
                DisplayName = "离线专属青锋",
                Level = 12,
                Quality = EquipmentQuality.Legendary,
                IsLocked = true,
                CreateTimeUtc = System.DateTime.UtcNow
            });
            localInventory.Materials.Add(new ItemStack { ItemId = "item_enhancement_stone", Count = 321 });
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                SoftCurrency = 98765,
                PremiumCurrency = 777,
                StageElapsedSeconds = 600d,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
                InventoryJson = InventoryStateCodec.Serialize(localInventory),
                EquippedInstanceIdsJson = "{\"ids\":[\"offline-only-equipment\"]}",
                ProgressJson = PlayerProgressStateCodec.Serialize(localProgress)
            });
            var saveBeforeServerEntry = File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath);

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport(requiredStartStageId: "stage_1_1");
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("fresh-server-ignores-local", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();

            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_1"),
                "A fresh server account must start from the server-authoritative stage, not local stage 1-7.");
            Assert.That(controller.PacingElapsedSecondsForTests, Is.EqualTo(0d),
                "Server reward pacing must start from a fresh server session, not the offline clock.");
            Assert.That(controller.SoftCurrencyForTests, Is.Zero,
                "A fresh server account must use the authoritative server balance, not high offline currency.");
            Assert.That(controller.PremiumCurrencyForTests, Is.Zero);
            Assert.That(controller.CommercialUnlocked, Is.False,
                "Offline equipment must not unlock server-session commercial navigation.");
            var serverRuntimeInventory = ReadRuntimeInventory(controller);
            Assert.That(serverRuntimeInventory.Equipment, Is.Empty,
                "A fresh server runtime must not inherit offline equipment.");
            Assert.That(serverRuntimeInventory.Materials, Is.Empty,
                "A fresh server runtime must not inherit offline materials.");
            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRequestBodies.Count < 1) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.BattleStartStageIds, Is.EqualTo(new[] { "stage_1_1" }));
            Assert.That(transport.FinishRequestBodies, Has.Count.EqualTo(1),
                "The authoritative stage should settle instead of looping on a locked local stage.");
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeServerEntry));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator InvalidServerProfile_ThenProductionOffline_RestoresCompleteLocalState()
        {
            DeleteLocalSave();
            var localProgress = new PlayerProgressState { CurrentStageId = "stage_1_7" };
            for (var stage = 1; stage <= 6; stage++) localProgress.Stage.ClearedStageIds.Add("stage_1_" + stage);
            var localInventory = new InventoryState { EquipmentCapacity = 120 };
            localInventory.Equipment.Add(new EquipmentInstance
            {
                InstanceId = "offline-restored-equipment",
                BaseId = "weapon_cloudsteel_blade",
                DisplayName = "归档青锋",
                Level = 9,
                Quality = EquipmentQuality.Legendary,
                IsLocked = true,
                CreateTimeUtc = System.DateTime.UtcNow
            });
            localInventory.Materials.Add(new ItemStack { ItemId = "item_enhancement_stone", Count = 88 });
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                SoftCurrency = 54321,
                PremiumCurrency = 654,
                StageElapsedSeconds = 345d,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
                InventoryJson = InventoryStateCodec.Serialize(localInventory),
                EquippedInstanceIdsJson = "{\"ids\":[\"offline-restored-equipment\"]}",
                ProgressJson = PlayerProgressStateCodec.Serialize(localProgress)
            });
            var saveBeforeEntry = File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath);
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;

            var client = new ImmortalLootApiClient(new ServerLoopTransport());
            var loginTask = client.LoginAsync("invalid-server-profile", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            var login = Object.FindAnyObjectByType<PrototypeLoginController>();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("SERVER_ENTRY_REJECTED:"));
            login.UseAuthenticatedClientForTests(
                client, CreateServerProfile("stage_missing"), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();

            Assert.That(controller.GameplayActive, Is.False);
            Assert.That(controller.HasActiveBattleForTests, Is.False);
            Assert.That(login.CanEnterOffline, Is.True, "A rejected server snapshot must leave both retry and offline entry available.");
            Assert.That(FindIncludingInactive("LoginPage").activeSelf, Is.True);
            Assert.That(File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeEntry));

            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.GameplayActive, Is.True);
            Assert.That(controller.ServerGameplayActive, Is.False);
            Assert.That(controller.StageNumber, Is.EqualTo(7));
            Assert.That(controller.PacingElapsedSecondsForTests, Is.EqualTo(345d).Within(0.01d));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(54321));
            Assert.That(controller.PremiumCurrencyForTests, Is.EqualTo(654));
            Assert.That(controller.CommercialUnlocked, Is.True,
                "The production offline button must restore local equipment after a rejected server profile.");
            var restoredInventory = ReadRuntimeInventory(controller);
            Assert.That(restoredInventory.Equipment.ConvertAll(item => item.InstanceId),
                Is.EqualTo(new[] { "offline-restored-equipment" }));
            Assert.That(restoredInventory.Materials, Has.Count.EqualTo(1));
            Assert.That(restoredInventory.Materials[0].ItemId, Is.EqualTo("item_enhancement_stone"));
            Assert.That(restoredInventory.Materials[0].Count, Is.EqualTo(88));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ExistingServerProgress_AdvancesFromAuthoritativeStageWithoutLocalClock()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_1" }, elapsedSeconds: 0d);
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport(requiredStartStageId: "stage_1_7");
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("existing-server-progress", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile("stage_1_7", ClearedStages(6)), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();

            Assert.That(controller.PacingElapsedSecondsForTests, Is.Zero);
            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_7"));
            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRequestBodies.Count < 1) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.BattleStartStageIds, Is.EqualTo(new[] { "stage_1_7" }));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_8"),
                "The server lock check, not the local demo clock, owns online stage advancement.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerProfileRefresh_AtomicallyRebuildsAuthoritativeStageMirror()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;

            var authoritativeProfile = CreateServerProfile("stage_1_3", ClearedStages(2));
            authoritativeProfile.realmStage = 10;
            authoritativeProfile.breakthroughMaterial = 1000;
            authoritativeProfile.pendingTribulation = new PendingTribulationDto
            {
                targetRealmId = "realm_qi_coalescence",
                reservedMaterial = 2000,
                requiredExperience = 1200
            };
            authoritativeProfile.spiritualRoots = new[]
            {
                new SpiritualRootProfileDto { rootId = "root_fire", level = 2, maxLevel = 10 }
            };
            var transport = new ServerLoopTransport(
                requiredStartStageId: "stage_1_1",
                authoritativeProfileCurrentStageId: "stage_1_3",
                authoritativeProfileClearedStageIds: ClearedStages(2),
                authoritativeProfile: authoritativeProfile);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("server-profile-reconciliation", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();

            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.ProfileRequestCount < 1) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.ProfileRequestCount, Is.EqualTo(1));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_3"),
                "The refreshed server profile must replace the client's stage-1-to-stage-2 prediction.");
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds,
                Is.EquivalentTo(new[] { "stage_1_1", "stage_1_2" }),
                "The refreshed currentStageId and clearedStageIds must be applied as one authoritative mirror.");
            Assert.That(controller.ProgressForTests.Realm.CultivationExperience, Is.EqualTo(900),
                "A refreshed server profile must preserve the authoritative cumulative cultivation pool instead of mirroring residual level experience.");
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial, Is.EqualTo(1000));
            Assert.That(controller.ProgressForTests.Realm.PendingTribulation, Is.Not.Null);
            Assert.That(controller.ProgressForTests.Realm.PendingTribulation.TargetRealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(controller.ProgressForTests.Realm.PendingTribulation.ReservedMaterial, Is.EqualTo(2000));
            Assert.That(controller.ProgressForTests.SpiritualRoots.Roots.Find(value => value.RootId == "root_fire")?.Level,
                Is.EqualTo(2), "A refreshed profile must replace the authoritative spiritual-root mirror together with realm state.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerCultivation_RefreshesAuthoritativeMaterialAndPendingWithoutClientTrialClaims()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;

            var initialProfile = CreateServerProfile();
            initialProfile.level = 10;
            initialProfile.realmStage = 10;
            initialProfile.cultivationExperience = 2000;
            initialProfile.breakthroughMaterial = 3000;
            var refreshedProfile = CreateServerProfile();
            refreshedProfile.level = 10;
            refreshedProfile.realmStage = 10;
            refreshedProfile.cultivationExperience = 2000;
            refreshedProfile.breakthroughMaterial = 1000;
            refreshedProfile.pendingTribulation = new PendingTribulationDto
            {
                targetRealmId = "realm_qi_coalescence",
                reservedMaterial = 2000,
                requiredExperience = 1200
            };
            var transport = new RealmActionTransport(refreshedProfile, new RealmBreakthroughDto
            {
                realmId = "realm_body_tempering",
                realmStage = 10,
                status = "TribulationRequired",
                targetRealmId = "realm_qi_coalescence",
                requiredLevel = 10,
                requiredExperience = 1200,
                requiredMaterial = 2000,
                materialSpent = 2000,
                breakthroughMaterial = 1000
            });
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("server-realm-action", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, initialProfile, CreateServerInventory());

            GameObject.Find("Nav_CultivationPage").GetComponent<Button>().onClick.Invoke();
            GameObject.Find("Action_CultivationPage").GetComponent<Button>().onClick.Invoke();
            var timeout = 2f;
            while (timeout > 0f && transport.ProfileRequestCount < 1) { timeout -= Time.deltaTime; yield return null; }

            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            var content = GameObject.Find("CultivationPageContent").GetComponent<Text>().text;
            Assert.That(transport.RealmRequestCount, Is.EqualTo(1));
            Assert.That(transport.ProfileRequestCount, Is.EqualTo(1), "A confirmed realm action must refresh the authoritative profile before returning UI control.");
            Assert.That(transport.RealmRequestBody, Does.Not.Contain("Token"));
            Assert.That(transport.RealmRequestBody, Does.Not.Contain("Victory"));
            Assert.That(transport.RealmRequestBody, Does.Not.Contain("RequiredLevel"));
            Assert.That(transport.RealmRequestBody, Does.Not.Contain("Material"));
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial, Is.EqualTo(1000));
            Assert.That(controller.ProgressForTests.Realm.PendingTribulation?.TargetRealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(controller.ProgressForTests.SpiritualRoots.Roots, Is.Empty,
                "An empty authoritative root list must not be filled with a client-created fire root.");
            Assert.That(content, Does.Contain("渡劫已开启"));
            Assert.That(content, Does.Contain("Boss"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerCultivation_UnknownResponseReusesIntentKeyOnSafeRetry()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;

            var refreshedProfile = CreateServerProfile();
            var transport = new RealmActionTransport(refreshedProfile, new RealmBreakthroughDto
            {
                realmId = "realm_body_tempering",
                realmStage = 2,
                status = "AdvancedStage"
            }, failFirstBreakthroughResponse: true);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("server-realm-retry", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());

            GameObject.Find("Nav_CultivationPage").GetComponent<Button>().onClick.Invoke();
            var action = GameObject.Find("Action_CultivationPage").GetComponent<Button>();
            action.onClick.Invoke();
            var timeout = 2f;
            while (timeout > 0f && transport.ProfileRequestCount < 1) { timeout -= Time.deltaTime; yield return null; }
            yield return null;
            action.onClick.Invoke();
            timeout = 2f;
            while (timeout > 0f && transport.ProfileRequestCount < 2) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.RealmRequestBodies.Count, Is.EqualTo(2));
            Assert.That(transport.RealmRequestBodies[1], Is.EqualTo(transport.RealmRequestBodies[0]),
                "An unknown response must retry the same server intent instead of creating a second breakthrough.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerMode_ClientPacingCannotAuthorizeNormalRewards()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport();
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("playmode-server", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller.GameplayActive, Is.True);
            Assert.That(controller.ServerGameplayActive, Is.True, "Authentication must commit a fixed server-authoritative session.");
            var loot = GameObject.Find("Loot").GetComponent<Text>();

            controller.AdvancePacingForTests(20d);
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);
            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRewardWindowEligibility.Count < 1) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.FinishRequestBodies[0], Does.Contain("\"RewardWindowEligible\""), "New clients must send the additive reward-window flag explicitly.");
            Assert.That(transport.FinishRewardWindowEligibility[0], Is.False);
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_1"), "A windowless authenticated victory must still advance the in-memory server mirror.");
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_2"), "A windowless victory may advance when the time gate is open.");
            Assert.That(controller.ServerLatestInstanceIdForTests, Is.Empty);
            Assert.That(transport.Paths, Does.Not.Contain("/player/inventory"), "A zero-equipment response must not trigger a fake inventory lookup.");
            Assert.That(loot.text, Does.Not.Contain("服务器掉落"));
            Assert.That(GameObject.Find("Currencies").GetComponent<Text>().text, Does.Contain("灵砂 0"), "Refreshing the profile after a windowless clear must retain zero soft-currency reward.");

            controller.RespawnCurrentBattleForTests();
            controller.AdvancePacingForTests(40d);
            Assert.That(controller.PendingRewardWindowsForTests, Is.EqualTo(1));
            controller.ResolveCurrentBattleForTests();
            timeout = 2f;
            while (timeout > 0f && transport.FinishRewardWindowEligibility.Count < 2) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.FinishRewardWindowEligibility[1], Is.False,
                "Until the server owns a persisted reward clock, client pacing must never authorize a normal-stage reward.");
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero,
                "The unsupported client-side window is explicitly consumed after a confirmed windowless settlement.");
            Assert.That(transport.Paths, Does.Contain("/battle/start"));
            Assert.That(transport.Paths, Does.Contain("/battle/finish"));
            Assert.That(transport.Paths, Does.Not.Contain("/player/inventory"));
            Assert.That(controller.ServerLatestInstanceIdForTests, Is.Empty);
            Assert.That(loot.text, Does.Not.Contain("服务器掉落"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerBossWithPendingTribulationRefreshesAuthoritativeResolutionWithoutClientClaims()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_1" });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var initialProfile = CreateServerProfile("stage_1_10", ClearedStages(9));
            initialProfile.level = 10;
            initialProfile.realmStage = 10;
            initialProfile.cultivationExperience = 2000;
            initialProfile.breakthroughMaterial = 1000;
            initialProfile.pendingTribulation = new PendingTribulationDto
            {
                targetRealmId = "realm_qi_coalescence",
                reservedMaterial = 2000,
                requiredExperience = 1200
            };
            var resolvedProfile = CreateServerProfile("stage_1_1", ClearedStages(10));
            resolvedProfile.level = 10;
            resolvedProfile.cultivationExperience = 1050;
            resolvedProfile.realmId = "realm_qi_coalescence";
            resolvedProfile.realmStage = 1;
            resolvedProfile.breakthroughMaterial = 1100;
            resolvedProfile.spiritualRoots = new[]
            {
                new SpiritualRootProfileDto { rootId = "root_fire", level = 1, maxLevel = 10 }
            };
            var transport = new ServerLoopTransport(authoritativeProfile: resolvedProfile, failInventory: true);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("playmode-server-boss", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, initialProfile, CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);

            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRewardWindowEligibility.Count < 1) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.FinishRewardWindowEligibility[0], Is.True, "An authenticated Boss must always use the rewarded finish path.");
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"));
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_10"));
            Assert.That(controller.ServerLatestInstanceIdForTests, Is.EqualTo("equip-online"));
            Assert.That(GameObject.Find("Loot").GetComponent<Text>().text, Does.Contain("装备背包同步失败"));
            Assert.That(controller.ProgressForTests.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(controller.ProgressForTests.Realm.RealmStage, Is.EqualTo(1));
            Assert.That(controller.ProgressForTests.Realm.CultivationExperience, Is.EqualTo(1050));
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial, Is.EqualTo(1100));
            Assert.That(controller.ProgressForTests.Realm.PendingTribulation, Is.Null);
            Assert.That(controller.ProgressForTests.SpiritualRoots.Roots.Find(value => value.RootId == "root_fire")?.Level, Is.EqualTo(1));
            Assert.That(GameObject.Find("GuideText").GetComponent<Text>().text, Does.Contain("当前权威境界"));
            Assert.That(GameObject.Find("GuideText").GetComponent<Text>().text, Does.Contain("凝气"));
            Assert.That(transport.FinishRequestBodies[0], Does.Not.Contain("Token"));
            Assert.That(transport.FinishRequestBodies[0], Does.Not.Contain("Victory"));
            Assert.That(transport.Paths, Does.Not.Contain("/realm/resolve"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerProfileRefresh_InvalidPendingStateLeavesAuthoritativeMirrorUnchanged()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;

            var invalidProfile = CreateServerProfile("stage_1_3", ClearedStages(2));
            invalidProfile.realmStage = 2;
            invalidProfile.breakthroughMaterial = 999;
            invalidProfile.pendingTribulation = new PendingTribulationDto
            {
                targetRealmId = "realm_missing",
                reservedMaterial = 2000,
                requiredExperience = 1200
            };
            var transport = new ServerLoopTransport(authoritativeProfile: invalidProfile);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("server-invalid-profile", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());

            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            var refresh = controller.RefreshServerProfileAsync();
            while (!refresh.IsCompleted) yield return null;
            Assert.That(refresh.Exception, Is.Not.Null);
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"));
            Assert.That(controller.ProgressForTests.Realm.RealmStage, Is.EqualTo(1));
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial, Is.Zero);
            Assert.That(controller.ProgressForTests.Realm.PendingTribulation, Is.Null);
            Assert.That(controller.ProgressForTests.SpiritualRoots.Roots, Is.Empty);
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ServerFinishUnknownFreezesOriginalStageAndWindowForConfirmation()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport(failBattleFinish: true);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("playmode-server-failure", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();

            controller.AdvancePacingForTests(60d);
            Assert.That(controller.PendingRewardWindowsForTests, Is.EqualTo(1));
            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRewardWindowEligibility.Count < 1) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.Paths, Does.Contain("/battle/finish"));
            Assert.That(transport.FinishRewardWindowEligibility[0], Is.False,
                "A failed normal-stage request must still carry the fail-closed reward decision.");
            Assert.That(controller.PendingRewardWindowsForTests, Is.EqualTo(1), "A failed finish must retain its pending reward window for retry.");
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"));
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Not.Contain("stage_1_1"));
            Assert.That(controller.DefeatsOnCurrentStageForTests, Is.Zero, "An unknown finish result is not a battle defeat.");
            Assert.That(controller.HasPendingServerSettlementForTests, Is.True);
            Assert.That(GameObject.Find("GuideText").GetComponent<Text>().text, Does.Contain("结算确认中"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator LostFinishResponseRetriesSameSettlementWithoutDuplicatingRewardWindow()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport(loseFirstFinishResponseAfterCommit: true);
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("playmode-server-response-loss", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile(), CreateServerInventory());
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.AdvancePacingForTests(60d);

            controller.ResolveCurrentBattleForTests();
            var timeout = 2f;
            while (timeout > 0f && transport.FinishRequestBodies.Count < 1) { timeout -= Time.deltaTime; yield return null; }
            Assert.That(controller.PendingRewardWindowsForTests, Is.EqualTo(1));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"));
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Not.Contain("stage_1_1"));
            Assert.That(transport.BattleStartCount, Is.EqualTo(1));
            Assert.That(transport.AuthoritativeFinishGrantCount, Is.EqualTo(1), "The simulated server committed the first finish before its response was lost.");
            Assert.That(controller.HasPendingServerSettlementForTests, Is.True);

            controller.RetryPendingServerSettlementForTests();
            timeout = 2f;
            while (timeout > 0f && transport.FinishRequestBodies.Count < 2) { timeout -= Time.deltaTime; yield return null; }

            Assert.That(transport.BattleStartCount, Is.EqualTo(1), "A response-loss retry must not create a second battle session.");
            Assert.That(transport.FinishRequestBodies[1], Is.EqualTo(transport.FinishRequestBodies[0]), "The retry must use the byte-equivalent finish intent.");
            Assert.That(transport.FinishSessionIds[1], Is.EqualTo(transport.FinishSessionIds[0]));
            Assert.That(transport.FinishIdempotencyKeys[1], Is.EqualTo(transport.FinishIdempotencyKeys[0]));
            Assert.That(transport.AuthoritativeFinishGrantCount, Is.EqualTo(1), "Replaying the same finish key must not create another authoritative grant.");
            Assert.That(controller.HasPendingServerSettlementForTests, Is.False);
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_1"));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_2"));
            DeleteLocalSave();
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

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var restored = EnterOfflineGameplay();
            Assert.That(restored.Power, Is.EqualTo(savedPower));
            Assert.That(restored.StageNumber, Is.EqualTo(savedStage));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator LocalSave_ReloadRestoresAggregateGrowthStateWithoutDuplicateTaskReward()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState
            {
                Realm = new RealmProgressState
                {
                    RealmId = "realm_body_tempering", RealmStage = 1, PlayerLevel = 1,
                    Experience = 0, CultivationExperience = 100, BreakthroughMaterial = 500
                }
            });
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.SetPacingSpeedForTests(240f);
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            var timeout = 5f;
            while (timeout > 0f && controller.LatestLoot == null)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.That(controller.LatestLoot, Is.Not.Null);
            controller.EquipLatest();
            Assert.That(controller.ExecutePageAction("CultivationPage"), Does.Contain("境界突破"));
            Assert.That(controller.ExecutePageAction("SpiritualRootPage"), Does.Contain("累计 1 点"));
            Assert.That(controller.ExecutePageAction("SpiritualRootPage"), Does.Contain("累计 2 点"));
            Assert.That(controller.ExecutePageAction("TaskPage"), Does.Contain("灵砂 +100"));
            controller.SaveForTests();

            var before = controller.ProgressForTests;
            Assert.That(before.CurrentStageId, Is.EqualTo($"stage_1_{controller.StageNumber}"));
            Assert.That(before.Realm.RealmStage, Is.EqualTo(2));
            Assert.That(before.Cultivation.PrimaryMethodId, Is.Not.Empty);
            Assert.That(before.SpiritualRoots.Roots.Find(value => value.RootId == "root_fire")?.Level, Is.EqualTo(2));
            Assert.That(before.GuideStep, Is.GreaterThanOrEqualTo(3));
            Assert.That(before.TaskClaimed, Is.True);
            var savedPower = controller.Power;

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var restored = EnterOfflineGameplay();
            var after = restored.ProgressForTests;
            Assert.That(after.CurrentStageId, Is.EqualTo(before.CurrentStageId));
            Assert.That(after.Stage.ClearedStageIds, Is.EquivalentTo(before.Stage.ClearedStageIds));
            Assert.That(after.Realm.RealmStage, Is.EqualTo(before.Realm.RealmStage));
            Assert.That(after.Cultivation.PrimaryMethodId, Is.EqualTo(before.Cultivation.PrimaryMethodId));
            Assert.That(after.Cultivation.AuxiliaryMethodIds, Is.EqualTo(before.Cultivation.AuxiliaryMethodIds));
            Assert.That(after.SpiritualRoots.Roots.Find(value => value.RootId == "root_fire")?.Level, Is.EqualTo(2));
            Assert.That(after.GuideStep, Is.EqualTo(before.GuideStep));
            Assert.That(after.TaskClaimed, Is.True);
            Assert.That(restored.Power, Is.EqualTo(savedPower), "Restored cultivation, roots and equipment must produce the same power.");
            var currencyBeforeDuplicateClaim = restored.SoftCurrencyForTests;
            Assert.That(restored.ExecutePageAction("TaskPage"), Does.Contain("已领取"));
            Assert.That(restored.SoftCurrencyForTests, Is.EqualTo(currencyBeforeDuplicateClaim),
                "A repeated task action after reload must not grant soft currency again.");
            Assert.That(restored.ExecutePageAction("SpiritualRootPage"), Does.Contain("累计 3 点"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator CultivationPage_ConsumesConfiguredRealmResources_AndRaisesPower()
        {
            DeleteLocalSave();
            var progress = new PlayerProgressState
            {
                Realm = new RealmProgressState
                {
                    RealmId = "realm_body_tempering",
                    RealmStage = 1,
                    PlayerLevel = 1,
                    Experience = 0,
                    CultivationExperience = 100,
                    BreakthroughMaterial = 500
                }
            };
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                Level = 1,
                Exp = 0,
                RealmId = progress.Realm.RealmId,
                RealmStage = progress.Realm.RealmStage,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(new InventoryState { EquipmentCapacity = 120 }),
                ProgressJson = PlayerProgressStateCodec.Serialize(progress)
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            var powerBefore = controller.Power;

            var result = controller.ExecutePageAction("CultivationPage");

            Assert.That(result, Does.Contain("境界突破至 2 阶"));
            Assert.That(controller.ProgressForTests.Realm.Experience, Is.Zero);
            Assert.That(controller.ProgressForTests.Realm.CultivationExperience, Is.EqualTo(90));
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial, Is.EqualTo(450));
            Assert.That(controller.Power, Is.GreaterThan(powerBefore),
                "The real realm stat provider must make a successful breakthrough visibly increase unified power.");
            controller.SaveForTests();

            var restored = PlayerProgressStateCodec.Deserialize(JsonPlayerSaveRepository.CreateDefault().Load().ProgressJson);
            Assert.That(restored.Realm.RealmStage, Is.EqualTo(2));
            Assert.That(restored.Realm.Experience, Is.Zero);
            Assert.That(restored.Realm.CultivationExperience, Is.EqualTo(90));
            Assert.That(restored.Realm.BreakthroughMaterial, Is.EqualTo(450));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator MajorBreakthrough_PersistsPendingTrial_AndBossResolvesItOnce()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState
            {
                CurrentStageId = "stage_1_10",
                Realm = new RealmProgressState
                {
                    RealmId = "realm_body_tempering", RealmStage = 10, PlayerLevel = 10,
                    Experience = 0, CultivationExperience = 2000, BreakthroughMaterial = 3000
                }
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            Assert.That(controller.ExecutePageAction("CultivationPage"), Does.Contain("渡劫已开启"));
            var pendingToken = controller.ProgressForTests.Realm.PendingTribulation?.Token;
            Assert.That(pendingToken, Is.Not.Empty);
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial, Is.EqualTo(1000));

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var restored = EnterOfflineGameplay();
            Assert.That(restored.ProgressForTests.Realm.PendingTribulation?.Token, Is.EqualTo(pendingToken),
                "A pending major breakthrough must survive a process-style scene reload.");
            Assert.That(restored.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_10"));
            var materialBeforeBoss = restored.ProgressForTests.Realm.BreakthroughMaterial;
            var experienceGrantedBeforeBoss = restored.ConfiguredStageExperienceGrantedForTests;
            var powerBeforeBoss = restored.Power;

            restored.ResolveCurrentBattleForTests();

            var settled = restored.ProgressForTests;
            Assert.That(settled.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(settled.Realm.RealmStage, Is.EqualTo(1));
            Assert.That(settled.Realm.PendingTribulation, Is.Null);
            Assert.That(settled.Realm.BreakthroughMaterial, Is.EqualTo(materialBeforeBoss + 100),
                "Trial resolution must not double-spend the reserved material and must still grant the Boss reward.");
            Assert.That(settled.Realm.CultivationExperience, Is.EqualTo(1050),
                "The trial must consume 1200 accumulated cultivation experience before the Boss reward adds 250.");
            Assert.That(settled.SpiritualRoots.GrantRecords.Count, Is.EqualTo(1));
            Assert.That(settled.SpiritualRoots.GrantRecords[0].TribulationToken, Is.EqualTo(pendingToken));
            Assert.That(settled.SpiritualRoots.Roots.FindAll(value => value.Level > 0).Count, Is.EqualTo(1),
                "A successful major breakthrough must grant exactly one idempotent spiritual-root level.");
            Assert.That(restored.ConfiguredStageExperienceGrantedForTests, Is.EqualTo(experienceGrantedBeforeBoss + 250));
            Assert.That(restored.Power, Is.GreaterThan(powerBeforeBoss));

            var saved = PlayerProgressStateCodec.Deserialize(JsonPlayerSaveRepository.CreateDefault().Load().ProgressJson);
            Assert.That(saved.Realm.RealmId, Is.EqualTo("realm_qi_coalescence"));
            Assert.That(saved.Realm.PendingTribulation, Is.Null);
            Assert.That(saved.Realm.BreakthroughMaterial, Is.EqualTo(materialBeforeBoss + 100));
            Assert.That(saved.Realm.CultivationExperience, Is.EqualTo(1050));
            Assert.That(saved.SpiritualRoots.GrantRecords.Count, Is.EqualTo(1));
            Assert.That(saved.SpiritualRoots.GrantRecords[0].TribulationToken, Is.EqualTo(pendingToken));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator SeededAggregateStageRemainsAuthoritativeWithoutRewritingElapsedTime()
        {
            DeleteLocalSave();
            var repository = JsonPlayerSaveRepository.CreateDefault();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_7" }, 0d);

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            Assert.That(controller.StageNumber, Is.EqualTo(7));
            Assert.That(controller.PacingElapsedSecondsForTests, Is.LessThan(1d),
                "Restoring stage 7 must not move pacing time into a synthetic stage band.");
            controller.SaveForTests();

            var saved = repository.Load();
            var savedProgress = PlayerProgressStateCodec.Deserialize(saved.ProgressJson);
            Assert.That(savedProgress.CurrentStageId, Is.EqualTo("stage_1_7"));
            Assert.That(savedProgress.Stage.ClearedStageIds,
                Is.EquivalentTo(new[] { "stage_1_1", "stage_1_2", "stage_1_3", "stage_1_4", "stage_1_5", "stage_1_6" }),
                "Migrating an authoritative stage 7 save must reconcile only its prerequisite stages.");
            Assert.That(saved.StageElapsedSeconds, Is.LessThan(1d));

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.That(EnterOfflineGameplay().StageNumber, Is.EqualTo(7));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator LocalVictoryWithoutRewardWindowWritesStageAndFirstClearOnly()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            var experienceBefore = controller.ExperienceForTests;
            var softCurrencyBefore = controller.SoftCurrencyForTests;
            var premiumCurrencyBefore = controller.PremiumCurrencyForTests;

            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_1"));
            Assert.That(controller.ExperienceForTests, Is.EqualTo(experienceBefore),
                "A normal victory before the timed reward window must not grant configured experience.");
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore),
                "A normal victory before the timed reward window must not grant configured soft currency.");
            Assert.That(controller.PremiumCurrencyForTests, Is.EqualTo(premiumCurrencyBefore + 10));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"),
                "Elapsed-time gate is still closed, so a victory clears stage 1 without advancing early.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator RepeatedSameStageVictoryWithoutRewardWindowDoesNotSaveEveryBattle()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            var savesBeforeVictory = controller.SaveOperationCountForTests;
            var experienceBefore = controller.ExperienceForTests;
            var softCurrencyBefore = controller.SoftCurrencyForTests;

            controller.ResolveCurrentBattleForTests();
            var savesAfterFirstClear = controller.SaveOperationCountForTests;
            Assert.That(savesAfterFirstClear, Is.EqualTo(savesBeforeVictory + 1),
                "The first clear is a durable checkpoint.");

            controller.RespawnCurrentBattleForTests();
            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"));
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);
            Assert.That(controller.ExperienceForTests, Is.EqualTo(experienceBefore));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore));
            Assert.That(controller.SaveOperationCountForTests, Is.EqualTo(savesAfterFirstClear),
                "A repeated same-stage victory without a reward window must remain in memory until pause or quit.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator NormalTimedRewardWindowAlwaysProducesEquipment()
        {
            DeleteLocalSave();
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_1"));
            var experienceBefore = controller.ExperienceForTests;
            var softCurrencyBefore = controller.SoftCurrencyForTests;
            controller.AdvancePacingForTests(60d);
            Assert.That(controller.PendingRewardWindowsForTests, Is.EqualTo(1));

            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.LatestLoot, Is.Not.Null,
                "A normal pacing reward window must use the guaranteed-equipment prototype table.");
            Assert.That(controller.ExperienceForTests, Is.EqualTo(experienceBefore + 25));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore + 10));
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_1"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator PendingBackpressureSkipsOnlyEquipmentAndPreservesConfiguredProgression()
        {
            DeleteLocalSave();
            var inventory = new InventoryState
            {
                EquipmentCapacity = 120,
                PendingEquipment = new EquipmentInstance
                {
                    InstanceId = "pending-before-window",
                    BaseId = "weapon_cloudsteel_blade",
                    DisplayName = "待领取旧装备",
                    Level = 1,
                    Quality = EquipmentQuality.Fine,
                    CreateTimeUtc = System.DateTime.UtcNow
                }
            };
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(inventory),
                ProgressJson = PlayerProgressStateCodec.Serialize(new PlayerProgressState())
            });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            var experienceBefore = controller.ExperienceForTests;
            var softCurrencyBefore = controller.SoftCurrencyForTests;
            controller.AdvancePacingForTests(60d);

            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.SkippedPendingRewardWindowsForTests, Is.EqualTo(1));
            Assert.That(controller.ExperienceForTests, Is.EqualTo(experienceBefore + 25),
                "Equipment backpressure must not remove the level growth needed to recover from a blocked Boss.");
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore + 10));
            Assert.That(controller.LatestLoot?.InstanceId, Is.EqualTo("pending-before-window"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator DefeatKeepsCurrentStageAndRecordsRetry()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_3" });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();

            controller.RecordDefeatForTests();

            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_3"));
            Assert.That(controller.DefeatsOnCurrentStageForTests, Is.EqualTo(1));
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Not.Contain("stage_1_3"));
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator ThirdOfflineDefeatRetreatsToFarmableStageAndPersistsRecovery()
        {
            DeleteLocalSave();
            var pending = new EquipmentInstance
            {
                InstanceId = "pending-during-boss-recovery",
                BaseId = "weapon_cloudsteel_blade",
                DisplayName = "待领取恢复装备",
                Level = 1,
                Quality = EquipmentQuality.Fine,
                CreateTimeUtc = System.DateTime.UtcNow
            };
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                StageElapsedSeconds = 180d,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(new InventoryState
                {
                    EquipmentCapacity = 120,
                    PendingEquipment = pending
                }),
                ProgressJson = PlayerProgressStateCodec.Serialize(
                    new PlayerProgressState { CurrentStageId = "stage_1_10" })
            });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();

            Assert.That(controller.ActivePlayerMaxHpForTests, Is.GreaterThan(0f).And.LessThan(9999f),
                "The production battle must use real progression stats instead of the former invincibility floor.");
            controller.ResolveCurrentBattleForTests();
            controller.RespawnCurrentBattleForTests();
            controller.ResolveCurrentBattleForTests();
            controller.RespawnCurrentBattleForTests();
            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_9"));
            Assert.That(controller.DefeatsOnCurrentStageForTests, Is.Zero);
            Assert.That(controller.SaveOperationCountForTests, Is.GreaterThan(0));
            Assert.That(controller.ProgressForTests.CurrentStageId, Is.EqualTo("stage_1_9"));
            var saved = JsonPlayerSaveRepository.CreateDefault().Load();
            Assert.That(PlayerProgressStateCodec.Deserialize(saved.ProgressJson).CurrentStageId, Is.EqualTo("stage_1_9"),
                "The farmable recovery stage must survive an app restart instead of reopening on the blocked Boss.");
            var configuredExperienceBeforeFarm = controller.ConfiguredStageExperienceGrantedForTests;
            var cultivationExperienceBeforeFarm = controller.ProgressForTests.Realm.CultivationExperience;
            var currencyBeforeFarm = controller.SoftCurrencyForTests;
            controller.RespawnCurrentBattleForTests();
            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_9"),
                "The production respawn path must actually create the recovery encounter.");
            controller.AdvancePacingForTests(5d);
            controller.ResolveCurrentBattleForTests();
            Assert.That(controller.ConfiguredStageExperienceGrantedForTests,
                Is.EqualTo(configuredExperienceBeforeFarm + 225));
            Assert.That(controller.ProgressForTests.Realm.CultivationExperience,
                Is.EqualTo(cultivationExperienceBeforeFarm + 225));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(currencyBeforeFarm + 10));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_10"),
                "A recovery farm victory must return to the Boss after granting real growth.");
            Assert.That(controller.LatestLoot?.InstanceId, Is.EqualTo(pending.InstanceId),
                "Recovery must preserve the older pending equipment while skipping only the new equipment window.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator BossVictoryDropsRareWithoutWindowAndLoopsToFirstStage()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState
            {
                CurrentStageId = "stage_1_10",
                Realm = new RealmProgressState { PlayerLevel = 5 }
            });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);
            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_10"));
            var configuredExperienceBefore = controller.ConfiguredStageExperienceGrantedForTests;
            var softCurrencyBefore = controller.SoftCurrencyForTests;
            var breakthroughMaterialBefore = controller.ProgressForTests.Realm.BreakthroughMaterial;
            var cultivationExperienceBefore = controller.ProgressForTests.Realm.CultivationExperience;

            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.LatestLoot, Is.Not.Null);
            Assert.That(controller.LatestLoot.Quality, Is.GreaterThanOrEqualTo(EquipmentQuality.Rare));
            Assert.That(controller.ConfiguredStageExperienceGrantedForTests, Is.EqualTo(configuredExperienceBefore + 250));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore + 25));
            Assert.That(controller.ProgressForTests.Realm.BreakthroughMaterial,
                Is.EqualTo(breakthroughMaterialBefore + 100),
                "The configured Boss reward must fund the first real realm breakthrough instead of leaving the resource loop disconnected.");
            Assert.That(controller.ProgressForTests.Realm.CultivationExperience,
                Is.EqualTo(cultivationExperienceBefore + 250),
                "Boss experience must accumulate as realm cultivation progress even when level experience rolls over.");
            Assert.That(controller.ProgressForTests.Stage.ClearedStageIds, Does.Contain("stage_1_10"));
            Assert.That(controller.CurrentStageIdForTests, Is.EqualTo("stage_1_1"),
                "A completed chapter Boss must loop back instead of leaving the runtime permanently on stage 10.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator PartialAndDoubleAuxiliaryCultivationSavesRemainSafeAndUsable()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState
            {
                Cultivation = new CultivationMethodState
                {
                    LearnedMethodIds = new List<string> { "method_cinder_scripture" }
                }
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var partial = EnterOfflineGameplay();
            Assert.That(partial.ProgressForTests.Cultivation.PrimaryMethodId, Is.Empty);
            Assert.That(partial.ExecutePageAction("CultivationPage"), Does.Contain("安全未装配"));
            Assert.That(partial.ProgressForTests.Cultivation.PrimaryMethodId, Is.Empty);
            Assert.That(partial.ProgressForTests.Realm.RealmStage, Is.EqualTo(1),
                "Opening cultivation without resources must never grant a free realm stage.");

            SaveSeededProgress(new PlayerProgressState
            {
                Cultivation = new CultivationMethodState
                {
                    LearnedMethodIds = new List<string> { "method_cinder_scripture" },
                    PrimaryMethodId = "method_cinder_scripture"
                }
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var primaryOnly = EnterOfflineGameplay();
            Assert.That(primaryOnly.ProgressForTests.Cultivation.PrimaryMethodId, Is.EqualTo("method_cinder_scripture"));
            Assert.That(primaryOnly.ExecutePageAction("CultivationPage"), Does.Contain("保留当前功法"));
            Assert.That(primaryOnly.ProgressForTests.Cultivation.PrimaryMethodId, Is.EqualTo("method_cinder_scripture"));

            SaveSeededProgress(new PlayerProgressState
            {
                Cultivation = new CultivationMethodState
                {
                    LearnedMethodIds = new List<string>
                    {
                        "method_cinder_scripture", "method_ember_breath",
                        "method_thunder_pulse", "method_quick_spark"
                    },
                    PrimaryMethodId = "method_cinder_scripture",
                    AuxiliaryMethodIds = new[] { "method_ember_breath", "method_quick_spark" }
                }
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var doubleAuxiliary = EnterOfflineGameplay();
            Assert.That(doubleAuxiliary.ExecutePageAction("CultivationPage"), Does.Contain("雷修暴击"));
            var restored = doubleAuxiliary.ProgressForTests.Cultivation;
            Assert.That(restored.PrimaryMethodId, Is.EqualTo("method_thunder_pulse"));
            Assert.That(restored.AuxiliaryMethodIds[0], Is.EqualTo("method_quick_spark"));
            Assert.That(restored.AuxiliaryMethodIds[1], Is.Empty,
                "Cycling a curated build must clear a duplicate target from the second auxiliary slot.");
            DeleteLocalSave();
        }

        [UnityTest]
        public IEnumerator FireRootRestoreIgnoresNullRowsAndStopsAtConfiguredMaximum()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState
            {
                SpiritualRoots = new SpiritualRootState
                {
                    Roots = new List<SpiritualRootProgress>
                    {
                        null,
                        new SpiritualRootProgress { RootId = "root_fire", Level = 20 }
                    }
                }
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            var powerBefore = controller.Power;
            Assert.That(controller.ExecutePageAction("SpiritualRootPage"), Does.Contain("已达上限"));
            Assert.That(controller.ProgressForTests.SpiritualRoots.Roots.Find(value => value != null && value.RootId == "root_fire")?.Level,
                Is.EqualTo(20));
            Assert.That(controller.Power, Is.EqualTo(powerBefore));
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
                StageElapsedSeconds = 0,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(inventory),
                ProgressJson = PlayerProgressStateCodec.Serialize(new PlayerProgressState
                {
                    CurrentStageId = "stage_1_10",
                    Realm = new RealmProgressState { PlayerLevel = 5 }
                })
            });

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_10"));
            controller.ResolveCurrentBattleForTests();
            controller.SetPacingSpeedForTests(240f);
            controller.ResumeBattleForTests();
            var timeout = 5f;
            InventoryState afterDrop = null;
            while (timeout > 0f && afterDrop?.PendingEquipment == null)
            {
                if (repository.Exists)
                    afterDrop = InventoryStateCodec.Deserialize(repository.Load().InventoryJson);
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.That(afterDrop?.PendingEquipment, Is.Not.Null,
                "A real Boss reward must enter the durable pending slot when the protected inventory is full.");
            var pendingId = afterDrop.PendingEquipment.InstanceId;
            Assert.That(afterDrop.PendingEquipment.Quality, Is.GreaterThanOrEqualTo(EquipmentQuality.Rare),
                "A real stage-10 victory must exercise the guaranteed Rare+ Boss table even without a pacing window.");
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

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var restored = EnterOfflineGameplay();
            Assert.That(restored.LatestLoot?.InstanceId, Is.EqualTo(pendingId));
            Assert.That(restored.PendingRewardWindowsForTests, Is.Zero,
                "Reloading must not resurrect or silently drop a different pending-window count.");
            var reloadedInventory = InventoryStateCodec.Deserialize(repository.Load().InventoryJson);
            Assert.That(reloadedInventory.PendingEquipment?.InstanceId, Is.EqualTo(pendingId));
            Assert.That(reloadedInventory.Equipment.Count, Is.EqualTo(120));
            Assert.That(reloadedInventory.Equipment.Exists(item => item.InstanceId == pendingId), Is.False,
                "The pending item must remain outside the full regular inventory until space is available.");

            var firstAction = restored.ExecutePageAction("InventoryPage");
            Assert.That(firstAction, Does.Contain("再次执行"),
                "Destroying a protected item must require an explicit second action.");
            var afterWarning = InventoryStateCodec.Deserialize(repository.Load().InventoryJson);
            Assert.That(afterWarning.PendingEquipment?.InstanceId, Is.EqualTo(pendingId));
            Assert.That(afterWarning.Equipment, Has.Count.EqualTo(120));

            var replacement = restored.ExecutePageAction("InventoryPage");
            Assert.That(replacement, Does.Contain("已牺牲"));
            var afterReplacement = InventoryStateCodec.Deserialize(repository.Load().InventoryJson);
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
            var saveBeforeEntry = File.ReadAllText(JsonPlayerSaveRepository.DefaultPath);
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var waitingController = Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(GameObject.Find("Currencies").GetComponent<Text>().text, Does.Not.Contain("灵砂 100 "),
                "The login screen must not read or expose the offline snapshot before explicit offline entry.");
            waitingController.PauseApplicationForTests();
            Assert.That(File.ReadAllText(JsonPlayerSaveRepository.DefaultPath), Is.EqualTo(saveBeforeEntry),
                "Pausing on login must not rewrite LastActive or consume the offline window.");

            var activeController = EnterOfflineGameplay();
            var firstClaimCurrency = GameObject.Find("Currencies").GetComponent<Text>().text;
            Assert.That(firstClaimCurrency, Does.Not.Contain("灵砂 100 "));
            activeController.SaveForTests();

            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            Assert.That(GameObject.Find("Currencies").GetComponent<Text>().text, Is.Not.EqualTo(firstClaimCurrency),
                "The login screen must stay on its neutral baseline until offline entry is chosen again.");
            EnterOfflineGameplay();
            yield return null;
            Assert.That(GameObject.Find("Currencies").GetComponent<Text>().text, Is.EqualTo(firstClaimCurrency),
                "Re-entering after the claimed checkpoint must not grant the same offline window twice.");
            DeleteLocalSave();
        }

        private static void DeleteLocalSave()
        {
            if (File.Exists(JsonPlayerSaveRepository.DefaultPath)) File.Delete(JsonPlayerSaveRepository.DefaultPath);
        }

        private static InventoryState ReadRuntimeInventory(PrototypeGameController controller)
        {
            var field = typeof(PrototypeGameController).GetField(
                "_inventory",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "The PlayMode fixture could not inspect the runtime inventory boundary.");
            var service = field.GetValue(controller) as InventoryService;
            Assert.That(service, Is.Not.Null);
            return service.State;
        }

        private static PrototypeGameController EnterOfflineGameplay()
        {
            var controller = Object.FindAnyObjectByType<PrototypeGameController>();
            var enter = GameObject.Find("EnterGameButton")?.GetComponent<Button>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(enter, Is.Not.Null, "Offline gameplay tests must use the production entry button.");
            enter.onClick.Invoke();
            Assert.That(controller.GameplayActive, Is.True);
            Assert.That(controller.ServerGameplayActive, Is.False);
            return controller;
        }

        private static PlayerProfileDto CreateServerProfile(string currentStageId = "stage_1_1", string[] clearedStageIds = null) =>
            new PlayerProfileDto
            {
                playerId = "p1",
                nickname = "在线修士",
                level = 1,
                exp = 7,
                cultivationExperience = 900,
                realmId = "realm_body_tempering",
                realmStage = 1,
                power = 180,
                currentStageId = currentStageId,
                clearedStageIds = clearedStageIds ?? System.Array.Empty<string>(),
                spiritualRoots = System.Array.Empty<SpiritualRootProfileDto>()
            };

        private static InventoryDto CreateServerInventory() => new InventoryDto
        {
            items = System.Array.Empty<InventoryItemDto>(),
            equipment = System.Array.Empty<EquipmentItemDto>()
        };

        private static string[] ClearedStages(int count)
        {
            var stages = new string[count];
            for (var index = 0; index < count; index++) stages[index] = "stage_1_" + (index + 1);
            return stages;
        }

        private static void SaveSeededProgress(PlayerProgressState progress, double elapsedSeconds = 0d)
        {
            JsonPlayerSaveRepository.CreateDefault().Save(new PlayerSaveSnapshot
            {
                StageElapsedSeconds = elapsedSeconds,
                LastActiveUnixSeconds = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InventoryJson = JsonUtility.ToJson(new InventoryState { EquipmentCapacity = 120 }),
                ProgressJson = PlayerProgressStateCodec.Serialize(progress)
            });
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        private sealed class RecordingValidationSink : IValidationEventSink
        {
            public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
            public void Write(ValidationEvent value) => Events.Add(value);
        }

        private sealed class ServerLoopTransport : IApiTransport
        {
            public readonly List<string> Paths = new List<string>();
            public readonly List<string> FinishRequestBodies = new List<string>();
            public readonly List<bool> FinishRewardWindowEligibility = new List<bool>();
            public readonly List<string> FinishSessionIds = new List<string>();
            public readonly List<string> FinishIdempotencyKeys = new List<string>();
            public readonly List<string> BattleStartStageIds = new List<string>();
            public int BattleStartCount { get; private set; }
            public int AuthoritativeFinishGrantCount { get; private set; }
            public int ProfileRequestCount { get; private set; }
            private readonly bool _failBattleFinish;
            private readonly bool _loseFirstFinishResponseAfterCommit;
            private readonly string _requiredStartStageId;
            private readonly string _authoritativeProfileCurrentStageId;
            private readonly string[] _authoritativeProfileClearedStageIds;
            private readonly PlayerProfileDto _authoritativeProfile;
            private readonly bool _failInventory;
            private bool _hasSuccessfulFinish;
            private bool _hasRewardedFinish;
            private bool _lostFirstFinishResponse;
            private string _currentSessionId = string.Empty;
            private string _currentStageId = "stage_1_1";
            private string _lastStartedStageId = string.Empty;
            private readonly List<string> _clearedStageIds = new List<string>();
            private readonly Dictionary<string, bool> _committedFinishEligibility = new Dictionary<string, bool>();

            public ServerLoopTransport(
                bool failBattleFinish = false,
                bool loseFirstFinishResponseAfterCommit = false,
                string requiredStartStageId = "",
                string authoritativeProfileCurrentStageId = "",
                string[] authoritativeProfileClearedStageIds = null,
                PlayerProfileDto authoritativeProfile = null,
                bool failInventory = false)
            {
                _failBattleFinish = failBattleFinish;
                _loseFirstFinishResponseAfterCommit = loseFirstFinishResponseAfterCommit;
                _requiredStartStageId = requiredStartStageId ?? string.Empty;
                _authoritativeProfileCurrentStageId = authoritativeProfileCurrentStageId ?? string.Empty;
                _authoritativeProfileClearedStageIds = authoritativeProfileClearedStageIds;
                _authoritativeProfile = authoritativeProfile;
                _failInventory = failInventory;
            }

            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                Paths.Add(request.Path);
                var finishRewardWindowEligible = false;
                if (request.Path == "/battle/finish")
                {
                    FinishRequestBodies.Add(request.JsonBody);
                    var finish = JsonUtility.FromJson<BattleFinishCapture>(request.JsonBody);
                    finishRewardWindowEligible = finish != null && finish.RewardWindowEligible;
                    FinishRewardWindowEligibility.Add(finishRewardWindowEligible);
                    FinishSessionIds.Add(finish?.SessionId ?? string.Empty);
                    FinishIdempotencyKeys.Add(finish?.IdempotencyKey ?? string.Empty);
                    if (_failBattleFinish)
                        return Task.FromResult(new ApiResponse(503, "{\"error\":\"settlement unavailable\"}"));
                    var finishKey = finish?.IdempotencyKey ?? string.Empty;
                    if (!_committedFinishEligibility.TryGetValue(finishKey, out var committedRewardWindowEligible))
                    {
                        committedRewardWindowEligible = finishRewardWindowEligible;
                        _committedFinishEligibility.Add(finishKey, committedRewardWindowEligible);
                        AuthoritativeFinishGrantCount++;
                        _hasSuccessfulFinish = true;
                        _hasRewardedFinish |= committedRewardWindowEligible;
                        ConfirmStage(_lastStartedStageId);
                    }
                    finishRewardWindowEligible = committedRewardWindowEligible;
                    if (_loseFirstFinishResponseAfterCommit && !_lostFirstFinishResponse)
                    {
                        _lostFirstFinishResponse = true;
                        return Task.FromResult(new ApiResponse(503, "{\"error\":\"response lost after commit\"}"));
                    }
                }
                string json;
                switch (request.Path)
                {
                    case "/auth/login": json = "{\"playerId\":\"p1\",\"accessToken\":\"token\",\"expiresAtUtc\":\"2030-01-01T00:00:00Z\",\"isNewPlayer\":true}"; break;
                    case "/battle/start":
                        BattleStartCount++;
                        _currentSessionId = System.Guid.NewGuid().ToString();
                        var start = JsonUtility.FromJson<BattleStartCapture>(request.JsonBody);
                        _lastStartedStageId = start?.StageId ?? string.Empty;
                        BattleStartStageIds.Add(start?.StageId ?? string.Empty);
                        if (_requiredStartStageId.Length > 0 && start?.StageId != _requiredStartStageId)
                            return Task.FromResult(new ApiResponse(409, "{\"error\":\"Stage is locked.\"}"));
                        json = "{\"sessionId\":\"" + _currentSessionId + "\",\"stageId\":\"" + (start?.StageId ?? string.Empty) + "\",\"status\":\"Started\"}";
                        break;
                    case "/battle/finish":
                        json = finishRewardWindowEligible
                            ? "{\"sessionId\":\"" + _currentSessionId + "\",\"status\":\"Finished\",\"rewardSoftCurrency\":10,\"rewardExp\":25,\"rewardBreakthroughMaterial\":100,\"equipmentInstanceId\":\"equip-online\",\"replayed\":false}"
                            : "{\"sessionId\":\"" + _currentSessionId + "\",\"status\":\"Finished\",\"rewardSoftCurrency\":0,\"rewardExp\":0,\"rewardBreakthroughMaterial\":0,\"equipmentInstanceId\":\"\",\"replayed\":false}";
                        break;
                    case "/player/inventory":
                        if (_failInventory) return Task.FromResult(new ApiResponse(503, "{\"error\":\"inventory unavailable\"}"));
                        json = "{\"items\":[],\"equipment\":[{\"instanceId\":\"equip-online\",\"baseId\":\"gloves_starseal\",\"slot\":\"Gloves\",\"level\":1,\"quality\":\"Rare\",\"isLocked\":false,\"isEquipped\":false,\"instanceJson\":\"{\\\"instanceId\\\":\\\"equip-online\\\",\\\"baseId\\\":\\\"gloves_starseal\\\",\\\"slot\\\":\\\"Gloves\\\",\\\"level\\\":1,\\\"quality\\\":\\\"Rare\\\",\\\"affixes\\\":[{\\\"id\\\":\\\"affix_attack\\\",\\\"value\\\":8.5}]}\"}]}";
                        break;
                    case "/equipment/equip": json = "{\"instanceId\":\"equip-online\",\"slot\":\"Gloves\",\"replaced\":false}"; break;
                    case "/player/profile":
                        ProfileRequestCount++;
                        json = JsonUtility.ToJson(_authoritativeProfile ?? new PlayerProfileDto
                        {
                            playerId = "p1",
                            nickname = "在线修士",
                            level = 1,
                            exp = _hasRewardedFinish ? 25 : 0,
                            cultivationExperience = _hasRewardedFinish ? 925 : 900,
                            realmId = "realm_body_tempering",
                            realmStage = 1,
                            breakthroughMaterial = _hasRewardedFinish ? 100 : 0,
                            power = 180,
                            softCurrency = _hasRewardedFinish ? 10 : 0,
                            premiumCurrency = _hasSuccessfulFinish ? 10 : 0,
                            currentStageId = _authoritativeProfileCurrentStageId.Length > 0
                                ? _authoritativeProfileCurrentStageId
                                : _currentStageId,
                            clearedStageIds = _authoritativeProfileClearedStageIds ?? _clearedStageIds.ToArray(),
                            spiritualRoots = System.Array.Empty<SpiritualRootProfileDto>()
                        });
                        break;
                    default: json = "{}"; break;
                }
                return Task.FromResult(new ApiResponse(200, json));
            }

            private void ConfirmStage(string stageId)
            {
                var parts = (stageId ?? string.Empty).Split('_');
                if (parts.Length != 3 || parts[0] != "stage" || parts[1] != "1" ||
                    !int.TryParse(parts[2], out var stageNumber) || stageNumber < 1 || stageNumber > 10)
                    return;
                for (var number = 1; number <= stageNumber; number++)
                {
                    var clearedStageId = "stage_1_" + number;
                    if (!_clearedStageIds.Contains(clearedStageId)) _clearedStageIds.Add(clearedStageId);
                }
                _currentStageId = stageNumber == 10 ? "stage_1_1" : "stage_1_" + (stageNumber + 1);
            }

            [System.Serializable]
            private sealed class BattleStartCapture
            {
                public string StageId;
            }

            [System.Serializable]
            private sealed class BattleFinishCapture
            {
                public string SessionId;
                public string IdempotencyKey;
                public bool RewardWindowEligible;
            }
        }

        private sealed class RealmActionTransport : IApiTransport
        {
            private readonly PlayerProfileDto _refreshedProfile;
            private readonly RealmBreakthroughDto _breakthrough;
            private readonly bool _failFirstBreakthroughResponse;
            public int RealmRequestCount { get; private set; }
            public int ProfileRequestCount { get; private set; }
            public string RealmRequestBody { get; private set; } = string.Empty;
            public List<string> RealmRequestBodies { get; } = new List<string>();

            public RealmActionTransport(PlayerProfileDto refreshedProfile, RealmBreakthroughDto breakthrough, bool failFirstBreakthroughResponse = false)
            {
                _refreshedProfile = refreshedProfile;
                _breakthrough = breakthrough;
                _failFirstBreakthroughResponse = failFirstBreakthroughResponse;
            }

            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                switch (request.Path)
                {
                    case "/auth/login":
                        return Task.FromResult(new ApiResponse(200,
                            "{\"playerId\":\"p1\",\"accessToken\":\"token\",\"expiresAtUtc\":\"2030-01-01T00:00:00Z\",\"isNewPlayer\":true}"));
                    case "/realm/breakthrough":
                        RealmRequestCount++;
                        RealmRequestBody = request.JsonBody;
                        RealmRequestBodies.Add(request.JsonBody);
                        if (_failFirstBreakthroughResponse && RealmRequestCount == 1)
                            return Task.FromException<ApiResponse>(new IOException("simulated response loss after commit"));
                        return Task.FromResult(new ApiResponse(200, JsonUtility.ToJson(_breakthrough)));
                    case "/player/profile":
                        ProfileRequestCount++;
                        return Task.FromResult(new ApiResponse(200, JsonUtility.ToJson(_refreshedProfile)));
                    default:
                        return Task.FromResult(new ApiResponse(200, "{}"));
                }
            }
        }
    }
}
