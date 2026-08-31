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
            _content.rectTransform.sizeDelta = new Vector2(700f, 560f);
            _content.rectTransform.anchoredPosition = new Vector2(0f, 155f);
            Configure(source, "Setting_Sound", "声音", new Vector2(-190f, -240f), ToggleSound);
            Configure(Clone(source), "Setting_Vibration", "震动", new Vector2(190f, -240f), ToggleVibration);
            Configure(Clone(source), "Setting_Save", "立即保存", new Vector2(-190f, -340f), SaveNow);
            Configure(Clone(source), "Setting_Legal", "隐私与协议", new Vector2(190f, -340f), ShowLegal);
            Configure(Clone(source), "Setting_AutoEquip", "自动换装", new Vector2(0f, -440f), ToggleAutoEquip);
            Configure(Clone(source), "Setting_PrivacyAccept", "允许验证", new Vector2(-190f, -540f), AcceptPrivacy);
            Configure(Clone(source), "Setting_PrivacyDecline", "仅离线", new Vector2(190f, -540f), DeclinePrivacy);
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
        private void ToggleAutoEquip() => _content.text = _game.ToggleAutoEquipSetting();
        private void AcceptPrivacy() => _content.text = _game.AcceptPrivacySetting();
        private void DeclinePrivacy() => _content.text = _game.DeclinePrivacySetting();

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (transform.name == name) return transform.gameObject;
            return null;
        }
    }
}
