using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ImmortalLoot.Analytics;
using ImmortalLoot.Debugging;
using ImmortalLoot.Player;
using ImmortalLoot.Settings;
using ImmortalLoot.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImmortalLoot.Tests.PlayMode
{
    public sealed class ProductPageNavigationTests
    {
        private static readonly (string PageName, string ExpectedStateLabel)[] ProductPages =
        {
            ("CharacterPage", "战力"),
            ("EquipmentPage", "装备"),
            ("InventoryPage", "背包"),
            ("CultivationPage", "境界"),
            ("SpiritualRootPage", "灵根"),
            ("StagePage", "轮"),
            ("ShopPage", "商店"),
            ("RankingPage", "战力"),
            ("MailPage", "飞简"),
            ("TaskPage", "成长"),
            ("ActivityPage", "挂机"),
            ("DebugPage", "设置")
        };

        private static readonly string[] PlaceholderCopy = { "MVP", "占位", "接口已就绪" };

        [UnityTest]
        public IEnumerator NavigatingProductPages_ShowsLiveReadOnlySummariesWithoutSideEffects()
        {
            var saveDirectory = Path.Combine(Path.GetTempPath(), "immortal-loot-page-summary-" + Guid.NewGuid().ToString("N"));
            var savePath = Path.Combine(saveDirectory, "save.json");
            Directory.CreateDirectory(saveDirectory);
            var validationSink = new RecordingValidationSink();
            var saveOverride = JsonPlayerSaveRepository.OverrideDefaultPathForTests(savePath);
            var validationOverride = PrototypeGameController.OverrideValidationSinkForTests(validationSink);
            var settingsStore = new TestGameSettingsStore();
            new GameSettingsService(settingsStore).AcceptPrivacy();
            var settingsOverride = GameSettingsService.OverrideRuntimeStoreForTests(settingsStore);
            try
            {
                PrototypeGameController.PauseNextBattleForTests();
                SceneManager.LoadScene("Main");
                yield return null;

                var controller = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                var navigation = UnityEngine.Object.FindAnyObjectByType<PrototypeNavigationController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(navigation, Is.Not.Null);
                GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
                yield return null;

                validationSink.Events.Clear();
                var progressBefore = PlayerProgressStateCodec.Serialize(controller.ProgressForTests);
                var softCurrencyBefore = controller.SoftCurrencyForTests;
                var premiumCurrencyBefore = controller.PremiumCurrencyForTests;
                var saveOperationsBefore = controller.SaveOperationCountForTests;
                var saveExistedBefore = File.Exists(savePath);
                var saveBytesBefore = saveExistedBefore ? File.ReadAllBytes(savePath) : Array.Empty<byte>();

                foreach (var page in ProductPages)
                {
                    navigation.Show(page.PageName);
                    var content = GameObject.Find(page.PageName + "Content")?.GetComponent<Text>();
                    Assert.That(content, Is.Not.Null, page.PageName + " must expose readable page content.");
                    Assert.That(content.text, Is.EqualTo(controller.GetPageSummary(page.PageName)),
                        page.PageName + " must render the current read-only state summary when opened.");
                    Assert.That(content.text, Does.Contain(page.ExpectedStateLabel));
                    Assert.That(content.text.Length, Is.GreaterThan(12), page.PageName + " must contain a meaningful state summary.");
                    foreach (var forbidden in PlaceholderCopy)
                        Assert.That(content.text, Does.Not.Contain(forbidden), page.PageName + " still contains placeholder copy.");
                }

                var taskSummary = controller.GetPageSummary("TaskPage");
                Assert.That(taskSummary, Does.Not.Contain("每日"));
                Assert.That(taskSummary, Does.Not.Contain("今日"));
                Assert.That(taskSummary, Does.Not.Contain("日更"));
                Assert.That(taskSummary, Does.Contain("一次性"));
                var shopSummary = controller.GetPageSummary("ShopPage");
                Assert.That(shopSummary, Does.Contain("不执行支付"));
                Assert.That(shopSummary, Does.Contain("不发放"));

                Assert.That(PlayerProgressStateCodec.Serialize(controller.ProgressForTests), Is.EqualTo(progressBefore));
                Assert.That(controller.SoftCurrencyForTests, Is.EqualTo(softCurrencyBefore));
                Assert.That(controller.PremiumCurrencyForTests, Is.EqualTo(premiumCurrencyBefore));
                Assert.That(controller.SaveOperationCountForTests, Is.EqualTo(saveOperationsBefore));
                Assert.That(validationSink.Events, Is.Empty, "Page navigation must not emit validation funnel events.");
                Assert.That(File.Exists(savePath), Is.EqualTo(saveExistedBefore));
                if (saveExistedBefore) Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(saveBytesBefore));
            }
            finally
            {
                var activeController = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                if (activeController != null) UnityEngine.Object.DestroyImmediate(activeController.gameObject);
                foreach (var recorder in UnityEngine.Object.FindObjectsByType<PlaytestTelemetryRecorder>(FindObjectsInactive.Include))
                    UnityEngine.Object.DestroyImmediate(recorder.gameObject);
                settingsOverride.Dispose();
                validationOverride.Dispose();
                saveOverride.Dispose();
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
            }
        }

        private sealed class RecordingValidationSink : IValidationEventSink
        {
            public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
            public void Write(ValidationEvent value) => Events.Add(value);
        }
    }
}
