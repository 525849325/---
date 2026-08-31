using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ImmortalLoot.Analytics;
using ImmortalLoot.Debugging;
using ImmortalLoot.Network;
using ImmortalLoot.Player;
using ImmortalLoot.Settings;
using ImmortalLoot.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace ImmortalLoot.Tests.PlayMode
{
    public sealed class PrivacyConsentTests
    {
        [UnityTest]
        public IEnumerator FirstLaunch_RequiresChoice_AndDeclineKeepsOfflinePrivate()
        {
            foreach (var recorder in UnityEngine.Object.FindObjectsByType<PlaytestTelemetryRecorder>(FindObjectsInactive.Include))
                UnityEngine.Object.DestroyImmediate(recorder.gameObject);

            var saveDirectory = Path.Combine(Path.GetTempPath(), "immortal-loot-privacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            var settingsStore = new TestGameSettingsStore();
            var installIdStore = new TestInstallIdStore();
            var sink = new RecordingSink();
            var saveOverride = JsonPlayerSaveRepository.OverrideDefaultPathForTests(Path.Combine(saveDirectory, "save.json"));
            var settingsOverride = GameSettingsService.OverrideRuntimeStoreForTests(settingsStore);
            var installIdOverride = AnonymousInstallIdProvider.OverrideRuntimeStoreForTests(installIdStore);
            var telemetryOverride = PrototypeGameController.OverrideValidationSinkForTests(sink);
            try
            {
                SceneManager.LoadScene("Main");
                yield return null;

                var settings = new GameSettingsService(settingsStore);
                var login = UnityEngine.Object.FindAnyObjectByType<PrototypeLoginController>();
                var controller = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                var enter = FindIncludingInactive("EnterGameButton").GetComponent<Button>();
                var server = FindIncludingInactive("ServerLoginButton").GetComponent<Button>();
                var accept = FindIncludingInactive("PrivacyAcceptButton").GetComponent<Button>();
                var decline = FindIncludingInactive("PrivacyDeclineButton").GetComponent<Button>();
                var legal = FindIncludingInactive("PrivacyLegalButton").GetComponent<Button>();

                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Unknown));
                Assert.That(login.CanEnterOffline, Is.False);
                Assert.That(enter.interactable, Is.False);
                Assert.That(server.interactable, Is.False);
                Assert.That(accept.gameObject.activeSelf, Is.True);
                Assert.That(decline.gameObject.activeSelf, Is.True);
                Assert.That(legal.gameObject.activeSelf, Is.True);
                AssertVerticalGap(accept.GetComponent<RectTransform>(), legal.GetComponent<RectTransform>(), 5f);
                AssertVerticalGap(legal.GetComponent<RectTransform>(), enter.GetComponent<RectTransform>(), 5f);
                AssertVerticalGap(enter.GetComponent<RectTransform>(), server.GetComponent<RectTransform>(), 5f);
                AssertVerticalGap(server.GetComponent<RectTransform>(), FindIncludingInactive("LoginFeedback").GetComponent<RectTransform>(), 5f);
                Assert.That(UnityEngine.Object.FindAnyObjectByType<PlaytestTelemetryRecorder>(), Is.Null);

                server.onClick.Invoke();
                yield return null;
                Assert.That(installIdStore.GetString("identity.anonymousInstallId"), Is.Empty,
                    "A forced click before consent must not create a stable anonymous identifier.");
                Assert.That(login.ApiClient, Is.Null);

                accept.onClick.Invoke();
                yield return null;
                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Accepted));
                Assert.That(login.CanEnterOffline, Is.True);
                Assert.That(enter.interactable, Is.True);

                var installId = AnonymousInstallIdProvider.CreateRuntime().GetOrCreate();
                Assert.That(installId, Has.Length.EqualTo(32));
                var transport = new LoginOnlyTransport();
                var client = new ImmortalLootApiClient(transport, () => settings.PrivacyAccepted);
                var loginTask = client.LoginAsync(installId, "隐私测试");
                while (!loginTask.IsCompleted) yield return null;
                Assert.That(loginTask.Exception, Is.Null);
                login.UseAuthenticatedClientForTests(
                    client,
                    CreateServerProfile(),
                    new InventoryDto { items = Array.Empty<InventoryItemDto>(), equipment = Array.Empty<EquipmentItemDto>() },
                    notifyAuthenticated: false);
                Assert.That(login.HasPreparedServerSession, Is.True);

                decline.onClick.Invoke();
                yield return null;
                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Declined));
                Assert.That(settings.PrivacyAccepted, Is.False);
                Assert.That(login.CanEnterOffline, Is.True);
                Assert.That(enter.interactable, Is.True);
                Assert.That(server.interactable, Is.False);
                Assert.That(installIdStore.GetString("identity.anonymousInstallId"), Is.Empty);
                Assert.That(login.ApiClient, Is.Null);
                Assert.That(login.HasPreparedServerSession, Is.False);
                Assert.That(transport.RequestCount, Is.EqualTo(1), "Withdrawal must not emit a cleanup or follow-up request.");
                Assert.That(UnityEngine.Object.FindAnyObjectByType<PlaytestTelemetryRecorder>(), Is.Null);

                enter.onClick.Invoke();
                yield return null;
                Assert.That(controller.GameplayActive, Is.True);
                Assert.That(controller.ServerGameplayActive, Is.False);
                Assert.That(sink.Events, Is.Empty, "Declining analytics must not weaken or block the offline core loop.");
            }
            finally
            {
                foreach (var recorder in UnityEngine.Object.FindObjectsByType<PlaytestTelemetryRecorder>(FindObjectsInactive.Include))
                    UnityEngine.Object.DestroyImmediate(recorder.gameObject);
                var controller = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller.gameObject);
                telemetryOverride.Dispose();
                installIdOverride.Dispose();
                settingsOverride.Dispose();
                saveOverride.Dispose();
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
            }
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        private static void AssertVerticalGap(RectTransform upper, RectTransform lower, float minimum)
        {
            var gap = upper.anchoredPosition.y - upper.rect.height * 0.5f -
                      (lower.anchoredPosition.y + lower.rect.height * 0.5f);
            Assert.That(gap, Is.GreaterThanOrEqualTo(minimum), upper.name + " overlaps " + lower.name);
        }

        private sealed class RecordingSink : IValidationEventSink
        {
            public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
            public void Write(ValidationEvent value) => Events.Add(value);
        }

        private static PlayerProfileDto CreateServerProfile() => new PlayerProfileDto
        {
            playerId = "privacy-player",
            nickname = "隐私测试",
            level = 1,
            realmId = "realm_body_tempering",
            realmStage = 1,
            currentStageId = "stage_1_1",
            clearedStageIds = Array.Empty<string>(),
            spiritualRoots = Array.Empty<SpiritualRootProfileDto>()
        };

        private sealed class LoginOnlyTransport : IApiTransport
        {
            public int RequestCount { get; private set; }
            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                RequestCount++;
                return Task.FromResult(new ApiResponse(200,
                    "{\"playerId\":\"privacy-player\",\"accessToken\":\"privacy-token\",\"expiresAtUtc\":\"2026-09-01T00:00:00Z\",\"isNewPlayer\":true}"));
            }
        }
    }
}
