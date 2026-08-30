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
        public IEnumerator FreshServerAccount_IgnoresAdvancedLocalStageAndPacing()
        {
            DeleteLocalSave();
            var localProgress = new PlayerProgressState { CurrentStageId = "stage_1_7" };
            for (var stage = 1; stage <= 6; stage++) localProgress.Stage.ClearedStageIds.Add("stage_1_" + stage);
            SaveSeededProgress(localProgress, elapsedSeconds: 600d);
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
        public IEnumerator InvalidServerProfile_StaysAtLoginAndPreservesOfflineSave()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_4" }, elapsedSeconds: 180d);
            var saveBeforeEntry = File.ReadAllBytes(JsonPlayerSaveRepository.DefaultPath);
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
        public IEnumerator ServerBossWithoutPendingWindowStillRequestsAuthoritativeReward()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_1" });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var transport = new ServerLoopTransport();
            var client = new ImmortalLootApiClient(transport);
            var loginTask = client.LoginAsync("playmode-server-boss", "在线修士");
            while (!loginTask.IsCompleted) yield return null;
            Assert.That(loginTask.Exception, Is.Null);
            Object.FindAnyObjectByType<PrototypeLoginController>().UseAuthenticatedClientForTests(
                client, CreateServerProfile("stage_1_10", ClearedStages(9)), CreateServerInventory());
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
            Assert.That(GameObject.Find("Loot").GetComponent<Text>().text, Does.Contain("服务器掉落"));
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
        public IEnumerator PendingBackpressureSkipsNormalWindowWithoutConfiguredRewards()
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
            Assert.That(controller.ExperienceForTests, Is.EqualTo(experienceBefore));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore));
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
        public IEnumerator BossVictoryDropsRareWithoutWindowAndLoopsToFirstStage()
        {
            DeleteLocalSave();
            SaveSeededProgress(new PlayerProgressState { CurrentStageId = "stage_1_10" });
            PrototypeGameController.PauseNextBattleForTests();
            SceneManager.LoadScene("Main");
            yield return null;
            var controller = EnterOfflineGameplay();
            Assert.That(controller.PendingRewardWindowsForTests, Is.Zero);
            Assert.That(controller.ActiveBattleStageIdForTests, Is.EqualTo("stage_1_10"));
            var configuredExperienceBefore = controller.ConfiguredStageExperienceGrantedForTests;
            var softCurrencyBefore = controller.SoftCurrencyForTests;

            controller.ResolveCurrentBattleForTests();

            Assert.That(controller.LatestLoot, Is.Not.Null);
            Assert.That(controller.LatestLoot.Quality, Is.GreaterThanOrEqualTo(EquipmentQuality.Rare));
            Assert.That(controller.ConfiguredStageExperienceGrantedForTests, Is.EqualTo(configuredExperienceBefore + 250));
            Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore + 25));
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
                ProgressJson = PlayerProgressStateCodec.Serialize(new PlayerProgressState { CurrentStageId = "stage_1_10" })
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
            private readonly bool _failBattleFinish;
            private readonly bool _loseFirstFinishResponseAfterCommit;
            private readonly string _requiredStartStageId;
            private bool _hasSuccessfulFinish;
            private bool _hasRewardedFinish;
            private bool _lostFirstFinishResponse;
            private string _currentSessionId = string.Empty;
            private readonly Dictionary<string, bool> _committedFinishEligibility = new Dictionary<string, bool>();

            public ServerLoopTransport(
                bool failBattleFinish = false,
                bool loseFirstFinishResponseAfterCommit = false,
                string requiredStartStageId = "")
            {
                _failBattleFinish = failBattleFinish;
                _loseFirstFinishResponseAfterCommit = loseFirstFinishResponseAfterCommit;
                _requiredStartStageId = requiredStartStageId ?? string.Empty;
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
                        BattleStartStageIds.Add(start?.StageId ?? string.Empty);
                        if (_requiredStartStageId.Length > 0 && start?.StageId != _requiredStartStageId)
                            return Task.FromResult(new ApiResponse(409, "{\"error\":\"Stage is locked.\"}"));
                        json = "{\"sessionId\":\"" + _currentSessionId + "\",\"stageId\":\"" + (start?.StageId ?? string.Empty) + "\",\"status\":\"Started\"}";
                        break;
                    case "/battle/finish":
                        json = finishRewardWindowEligible
                            ? "{\"sessionId\":\"" + _currentSessionId + "\",\"status\":\"Finished\",\"rewardSoftCurrency\":10,\"rewardExp\":25,\"equipmentInstanceId\":\"equip-online\",\"replayed\":false}"
                            : "{\"sessionId\":\"" + _currentSessionId + "\",\"status\":\"Finished\",\"rewardSoftCurrency\":0,\"rewardExp\":0,\"equipmentInstanceId\":\"\",\"replayed\":false}";
                        break;
                    case "/player/inventory": json = "{\"items\":[],\"equipment\":[{\"instanceId\":\"equip-online\",\"baseId\":\"gloves_starseal\",\"slot\":\"Gloves\",\"level\":1,\"quality\":\"Rare\",\"isLocked\":false,\"isEquipped\":false,\"instanceJson\":\"{\\\"instanceId\\\":\\\"equip-online\\\",\\\"baseId\\\":\\\"gloves_starseal\\\",\\\"slot\\\":\\\"Gloves\\\",\\\"level\\\":1,\\\"quality\\\":\\\"Rare\\\",\\\"affixes\\\":[{\\\"id\\\":\\\"affix_attack\\\",\\\"value\\\":8.5}]}\"}]}"; break;
                    case "/equipment/equip": json = "{\"instanceId\":\"equip-online\",\"slot\":\"Gloves\",\"replaced\":false}"; break;
                    case "/player/profile":
                        json = "{\"playerId\":\"p1\",\"nickname\":\"在线修士\",\"level\":1,\"exp\":" + (_hasRewardedFinish ? 25 : 0) +
                               ",\"realmId\":\"realm_body_tempering\",\"realmStage\":1,\"power\":180,\"softCurrency\":" + (_hasRewardedFinish ? 10 : 0) +
                               ",\"premiumCurrency\":" + (_hasSuccessfulFinish ? 10 : 0) +
                               ",\"currentStageId\":\"stage_1_1\",\"clearedStageIds\":[],\"spiritualRoots\":[]}";
                        break;
                    default: json = "{}"; break;
                }
                return Task.FromResult(new ApiResponse(200, json));
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
    }
}
