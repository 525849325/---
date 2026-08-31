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
using UnityEngine.EventSystems;
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
            IDisposable transportOverride = null;
            var developmentSupportOverride = PrototypeLoginController.OverrideDevelopmentServerSupportForTests(true);
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

                Click(accept);
                yield return null;
                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Accepted));
                Assert.That(login.CanEnterOffline, Is.True);
                Assert.That(enter.interactable, Is.True);
                Assert.That(server.gameObject.activeSelf, Is.True);
                Assert.That(server.interactable, Is.True);

                Assert.That(accept.gameObject.activeSelf, Is.False);
                Assert.That(decline.gameObject.activeSelf, Is.True,
                    "Accepted users must retain a visible withdrawal action.");
                Assert.That(decline.interactable, Is.True,
                    "Withdrawal must remain available while a server request is pending.");
                Assert.That(decline.GetComponentInChildren<Text>(true).text, Does.Contain("撤回"));
                Assert.That(legal.gameObject.activeSelf, Is.True);

                var transport = new BlockingLoginTransport();
                transportOverride = PrototypeLoginController.OverrideTransportForTests(_ => transport);
                Click(server);
                yield return null;
                Assert.That(transport.RequestCount, Is.EqualTo(1));
                Assert.That(installIdStore.GetString("identity.anonymousInstallId"), Has.Length.EqualTo(32));
                Assert.That(login.CanEnterOffline, Is.False, "The login request should still be pending.");
                Assert.That(decline.gameObject.activeSelf, Is.True);
                Assert.That(decline.interactable, Is.True);

                Click(decline);
                yield return null;
                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Declined));
                Assert.That(settings.PrivacyAccepted, Is.False);
                Assert.That(login.CanEnterOffline, Is.True);
                Assert.That(enter.interactable, Is.True);
                Assert.That(server.interactable, Is.False);
                Assert.That(accept.gameObject.activeSelf, Is.True,
                    "A declined user must be able to make a later explicit acceptance choice.");
                Assert.That(decline.gameObject.activeSelf, Is.False);
                Assert.That(legal.gameObject.activeSelf, Is.True);
                Assert.That(installIdStore.GetString("identity.anonymousInstallId"), Is.Empty);
                Assert.That(login.ApiClient, Is.Null);
                Assert.That(login.HasPreparedServerSession, Is.False);
                Assert.That(transport.RequestCount, Is.EqualTo(1), "Withdrawal must not emit a cleanup or follow-up request.");
                Assert.That(UnityEngine.Object.FindAnyObjectByType<PlaytestTelemetryRecorder>(), Is.Null);

                Click(accept);
                yield return null;
                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Accepted));
                transport.CompleteLogin();
                yield return null;
                yield return null;
                Assert.That(transport.RequestCount, Is.EqualTo(1),
                    "A withdrawn login generation must not emit profile or inventory requests after later re-consent.");
                Assert.That(login.HasPreparedServerSession, Is.False);

                Click(decline);
                yield return null;
                Assert.That(settings.PrivacyConsent, Is.EqualTo(PrivacyConsentState.Declined));
                Assert.That(UnityEngine.Object.FindAnyObjectByType<PlaytestTelemetryRecorder>(), Is.Null);

                Click(enter);
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
                transportOverride?.Dispose();
                developmentSupportOverride.Dispose();
                telemetryOverride.Dispose();
                installIdOverride.Dispose();
                settingsOverride.Dispose();
                saveOverride.Dispose();
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulLogin_KeepsGenerationBoundClientUsableAfterServerEntry()
        {
            var saveDirectory = Path.Combine(Path.GetTempPath(), "immortal-loot-server-consent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            var settingsStore = new TestGameSettingsStore();
            var installIdStore = new TestInstallIdStore();
            var saveOverride = JsonPlayerSaveRepository.OverrideDefaultPathForTests(Path.Combine(saveDirectory, "save.json"));
            var settingsOverride = GameSettingsService.OverrideRuntimeStoreForTests(settingsStore);
            var installIdOverride = AnonymousInstallIdProvider.OverrideRuntimeStoreForTests(installIdStore);
            var supportOverride = PrototypeLoginController.OverrideDevelopmentServerSupportForTests(true);
            var transport = new SuccessfulLoginTransport();
            IDisposable transportOverride = null;
            try
            {
                SceneManager.LoadScene("Main");
                yield return null;
                yield return null;

                var login = UnityEngine.Object.FindAnyObjectByType<PrototypeLoginController>();
                transportOverride = PrototypeLoginController.OverrideTransportForTests(_ => transport);
                Click(FindIncludingInactive("PrivacyAcceptButton").GetComponent<Button>());
                Click(FindIncludingInactive("ServerLoginButton").GetComponent<Button>());
                for (var frame = 0; frame < 20 && !login.HasPreparedServerSession && !login.IsServerAuthenticated; frame++)
                    yield return null;

                Assert.That(transport.RequestCount, Is.EqualTo(3));
                if (login.HasPreparedServerSession)
                    Assert.That(login.TryCommitServerEntry(), Is.True);
                Assert.That(login.IsServerAuthenticated, Is.True,
                    FindIncludingInactive("LoginFeedback").GetComponent<Text>().text);

                var profileTask = login.ApiClient.GetProfileAsync();
                while (!profileTask.IsCompleted) yield return null;
                Assert.That(profileTask.Exception, Is.Null,
                    "The generation-bound consent gate must remain valid for the active server session.");
                Assert.That(transport.RequestCount, Is.EqualTo(4));
            }
            finally
            {
                foreach (var recorder in UnityEngine.Object.FindObjectsByType<PlaytestTelemetryRecorder>(FindObjectsInactive.Include))
                    UnityEngine.Object.DestroyImmediate(recorder.gameObject);
                var controller = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller.gameObject);
                transportOverride?.Dispose();
                supportOverride.Dispose();
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

        private static void Click(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name + " must be visible to a user.");
            Assert.That(button.interactable, Is.True, button.name + " must be interactable for a user.");
            Canvas.ForceUpdateCanvases();
            var eventSystem = EventSystem.current;
            var canvas = button.GetComponentInParent<Canvas>();
            var raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(raycaster, Is.Not.Null);

            var rect = button.GetComponent<RectTransform>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            Assert.That(screenPoint.x, Is.InRange(0f, (float)Screen.width));
            Assert.That(screenPoint.y, Is.InRange(0f, (float)Screen.height));

            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPoint
            };
            var hits = new List<RaycastResult>();
            raycaster.Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " must be hit by the UI raycaster.");
            Assert.That(hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform), Is.True,
                button.name + " must be the topmost raycast target at its center.");
            Assert.That(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.Not.Null,
                button.name + " must receive a pointer click through the EventSystem.");
        }

        private sealed class RecordingSink : IValidationEventSink
        {
            public readonly List<ValidationEvent> Events = new List<ValidationEvent>();
            public void Write(ValidationEvent value) => Events.Add(value);
        }

        private sealed class BlockingLoginTransport : IApiTransport
        {
            private readonly TaskCompletionSource<ApiResponse> _login = new TaskCompletionSource<ApiResponse>();
            public int RequestCount { get; private set; }
            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                RequestCount++;
                return _login.Task;
            }

            public void CompleteLogin() => _login.TrySetResult(new ApiResponse(200,
                "{\"playerId\":\"privacy-player\",\"accessToken\":\"privacy-token\",\"expiresAtUtc\":\"2026-09-01T00:00:00Z\",\"isNewPlayer\":true}"));
        }

        private sealed class SuccessfulLoginTransport : IApiTransport
        {
            public int RequestCount { get; private set; }

            public Task<ApiResponse> SendAsync(ApiRequest request)
            {
                RequestCount++;
                if (request.Path == "/auth/login")
                    return Task.FromResult(new ApiResponse(200,
                        "{\"playerId\":\"privacy-player\",\"accessToken\":\"privacy-token\",\"expiresAtUtc\":\"2026-09-01T00:00:00Z\",\"isNewPlayer\":true}"));
                if (request.Path == "/player/profile")
                    return Task.FromResult(new ApiResponse(200,
                        "{\"playerId\":\"privacy-player\",\"nickname\":\"隐私测试\",\"level\":1,\"realmId\":\"realm_body_tempering\",\"realmStage\":1,\"currentStageId\":\"stage_1_1\",\"clearedStageIds\":[],\"spiritualRoots\":[]}"));
                if (request.Path == "/player/inventory")
                    return Task.FromResult(new ApiResponse(200, "{\"items\":[],\"equipment\":[]}"));
                return Task.FromResult(new ApiResponse(404, "{}"));
            }
        }
    }
}
