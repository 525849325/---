using UnityEngine;
using UnityEngine.UI;

namespace ImmortalLoot.UI
{
    public sealed class PrototypeSettingsPanelController : MonoBehaviour
    {
        private PrototypeGameController _game;
        private Text _content;
        private bool _initialized;

        public void Initialize(PrototypeGameController game)
        {
            if (_initialized || game == null) return;
            var source = FindIncludingInactive("Action_DebugPage")?.GetComponent<Button>();
            _content = FindIncludingInactive("DebugPageContent")?.GetComponent<Text>();
            if (source == null || _content == null) return;
            _initialized = true;
            _game = game;
            Configure(source, "Setting_Sound", "声音", new Vector2(-190f, -350f), ToggleSound);
            Configure(Clone(source), "Setting_Vibration", "震动", new Vector2(190f, -350f), ToggleVibration);
            Configure(Clone(source), "Setting_Save", "立即保存", new Vector2(-190f, -470f), SaveNow);
            Configure(Clone(source), "Setting_Legal", "隐私与协议", new Vector2(190f, -470f), ShowLegal);
            _content.text = _game.SettingsSummary();
        }

        private static Button Clone(Button source) => Instantiate(source, source.transform.parent);

        private static void Configure(Button button, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            button.name = name;
            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(340f, 90f);
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void ToggleSound() => _content.text = _game.ToggleSoundSetting();
        private void ToggleVibration() => _content.text = _game.ToggleVibrationSetting();
        private void SaveNow() => _content.text = _game.SaveNowFromSettings();
        private void ShowLegal() => _content.text = _game.LegalNotice();

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (transform.name == name) return transform.gameObject;
            return null;
        }
    }
}
