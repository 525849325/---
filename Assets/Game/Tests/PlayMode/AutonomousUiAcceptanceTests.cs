using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImmortalLoot.Tests.PlayMode
{
    public sealed class AutonomousUiAcceptanceTests
    {
        private static readonly (string File, string Button)[] Pages =
        {
            ("02-battle", null), ("03-character", "Nav_CharacterPage"),
            ("04-inventory", "Nav_InventoryPage"), ("05-equipment", "Nav_EquipmentPage"),
            ("06-cultivation", "Nav_CultivationPage"), ("07-shop", "Nav_ShopPage"),
            ("08-task", "Nav_TaskPage"), ("09-ranking", "Nav_RankingPage"),
            ("10-activity", "Nav_ActivityPage"), ("11-stage", "Nav_StagePage")
        };

        [UnityTest]
        public IEnumerator CriticalPages_AreCapturedAndPassStructuralAudit()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "UI"));
            Directory.CreateDirectory(output);
            yield return Capture(Path.Combine(output, "01-login.png"));
            GameObject.Find("EnterGameButton").GetComponent<Button>().onClick.Invoke();
            yield return null;

            var issues = new List<string>();
            foreach (var page in Pages)
            {
                if (!string.IsNullOrEmpty(page.Button))
                    GameObject.Find(page.Button).GetComponent<Button>().onClick.Invoke();
                yield return null;
                AuditVisibleUi(issues);
                yield return Capture(Path.Combine(output, page.File + ".png"));
            }

            var json = "{\n  \"passed\": " + (issues.Count == 0 ? "true" : "false") +
                       ",\n  \"issueCount\": " + issues.Count + ",\n  \"issues\": [";
            for (var i = 0; i < issues.Count; i++)
                json += (i == 0 ? "" : ",") + "\n    \"" + Escape(issues[i]) + "\"";
            json += "\n  ]\n}";
            File.WriteAllText(Path.Combine(output, "ui-audit.json"), json);
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        private static IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path, 1);
            var timeout = 120;
            while (!File.Exists(path) && timeout-- > 0) yield return null;
            Assert.That(File.Exists(path), Is.True, "Screenshot was not written: " + path);
        }

        private static void AuditVisibleUi(List<string> issues)
        {
            foreach (var graphic in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
            {
                if (!graphic.isActiveAndEnabled || graphic.canvasRenderer.GetAlpha() <= 0.01f) continue;
                var corners = new Vector3[4];
                graphic.rectTransform.GetWorldCorners(corners);
                var canvas = graphic.canvas;
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                for (var i = 0; i < corners.Length; i++) corners[i] = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                var minX = Mathf.Min(corners[0].x, corners[2].x);
                var maxX = Mathf.Max(corners[0].x, corners[2].x);
                var minY = Mathf.Min(corners[0].y, corners[2].y);
                var maxY = Mathf.Max(corners[0].y, corners[2].y);
                if (minX < -1 || minY < -1 || maxX > Screen.width + 1 || maxY > Screen.height + 1)
                    issues.Add(graphic.name + " is outside the visible screen.");

                var text = graphic as Text;
                if (text != null && !string.IsNullOrWhiteSpace(text.text) &&
                    (text.preferredWidth > text.rectTransform.rect.width + 2 || text.preferredHeight > text.rectTransform.rect.height + 2))
                    issues.Add(text.name + " text is clipped.");
            }
            foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                var size = button.GetComponent<RectTransform>().rect.size;
                if (size.x < 44 || size.y < 44) issues.Add(button.name + " touch target is smaller than 44px.");
            }
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }
}
