using UnityEngine;
using UnityEngine.UI;
using ImmortalLoot.Equipment;

namespace ImmortalLoot.UI
{
    public static class PrototypeVisualTheme
    {
        public static readonly Color Ink = Hex("101820");
        public static readonly Color Surface = Hex("17242B");
        public static readonly Color SurfaceRaised = Hex("22343B");
        public static readonly Color Jade = Hex("4EC9A1");
        public static readonly Color Gold = Hex("E5B85C");
        public static readonly Color TextPrimary = Hex("F4E9D7");
        public static readonly Color TextMuted = Hex("A7B7B0");

        public static void Apply(Canvas canvas)
        {
            if (canvas == null) return;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            var background = canvas.GetComponent<Image>() ?? canvas.gameObject.AddComponent<Image>();
            background.color = Ink;
            background.raycastTarget = false;

            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                text.color = TextPrimary;
                text.supportRichText = true;
            }
            foreach (var button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                StyleButton(button);
            foreach (var pageName in new[] { "BattlePage", "CharacterPage", "EquipmentPage", "InventoryPage", "CultivationPage", "SpiritualRootPage", "StagePage", "ShopPage", "RankingPage", "MailPage", "TaskPage", "ActivityPage", "DebugPage" })
            {
                var page = FindIncludingInactive(pageName);
                var image = page == null ? null : page.GetComponent<Image>();
                if (image != null) image.color = pageName == "BattlePage" ? Surface : SurfaceRaised;
            }
            var health = FindIncludingInactive("EnemyHealth")?.GetComponent<Slider>();
            if (health?.fillRect != null)
            {
                var fill = health.fillRect.GetComponent<Image>();
                if (fill != null) fill.color = Hex("D56A5E");
            }
            var camera = Camera.main;
            if (camera != null) camera.backgroundColor = Ink;
            SetLabel("Nav_DebugPage", "设置");
            SetLabel("Action_DebugPage", "切换设置");
            SetLabel("LoginTitle", "《太初：无尽轮回》\n一念入青崖，万器皆有缘");
            var settings = canvas.GetComponent<PrototypeSettingsPanelController>() ?? canvas.gameObject.AddComponent<PrototypeSettingsPanelController>();
            settings.Initialize(Object.FindAnyObjectByType<PrototypeGameController>());
        }

        public static Color QualityColor(EquipmentQuality quality)
        {
            switch (quality)
            {
                case EquipmentQuality.Fine: return Hex("75C995");
                case EquipmentQuality.Rare: return Hex("62A7E8");
                case EquipmentQuality.Epic: return Hex("B68AE0");
                case EquipmentQuality.Legendary: return Hex("F0A04B");
                case EquipmentQuality.Mythic: return Gold;
                default: return TextPrimary;
            }
        }

        private static void StyleButton(Button button)
        {
            var image = button.GetComponent<Image>();
            if (image == null) return;
            var isPrimary = button.name == "EquipLatestButton" || button.name == "EnterGameButton";
            image.color = isPrimary ? Jade : SurfaceRaised;
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = isPrimary ? Hex("73D8B6") : Hex("31474F");
            colors.pressedColor = isPrimary ? Hex("37A984") : Hex("0D1419");
            colors.disabledColor = Hex("435057");
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.color = isPrimary ? Ink : TextPrimary;
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        private static void SetLabel(string objectName, string value)
        {
            var target = FindIncludingInactive(objectName);
            var label = target == null ? null : target.GetComponentInChildren<Text>(true);
            if (label != null) label.text = value;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
