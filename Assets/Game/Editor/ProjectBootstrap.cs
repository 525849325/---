using ImmortalLoot.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImmortalLoot.Editor
{
    public static class ProjectBootstrap
    {
        [MenuItem("ImmortalLoot/Build Prototype Scene")]
        public static void BuildPrototypeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10);
            camera.GetComponent<Camera>().backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvas = new GameObject("PrototypeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvasComponent = canvas.GetComponent<Canvas>();
            canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            var topBar = CreateImage(canvas.transform, "TopBar", new Color(0.07f, 0.11f, 0.15f, 0.96f));
            topBar.rectTransform.sizeDelta = new Vector2(1080, 150);
            topBar.rectTransform.anchoredPosition = new Vector2(0, 885);
            var profile = CreateText(topBar.transform, "Profile", "云游剑客  Lv.1\n战力 128", 28, new Vector2(-350, 0), new Vector2(300, 120));
            profile.alignment = TextAnchor.MiddleLeft;
            CreateText(topBar.transform, "Currencies", "灵砂 2,000    仙晶 60", 30, new Vector2(250, 0), new Vector2(480, 100));
            var header = CreateText(canvas.transform, "PageHeader", "青崖历练", 36, new Vector2(0, 770), new Vector2(700, 80));
            header.color = new Color(0.9f, 0.79f, 0.43f);

            var title = CreateText(canvas.transform, "Title", "《太初：无尽轮回》\nProject ImmortalLoot", 52, new Vector2(0, 650), new Vector2(900, 180));
            title.color = new Color(0.9f, 0.79f, 0.43f);
            var status = CreateText(canvas.transform, "BattleStatus", "正在准备战斗……", 42, new Vector2(0, 250), new Vector2(900, 300));
            var sliderObject = new GameObject("EnemyHealth", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvas.transform, false);
            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(760, 50);
            sliderRect.anchoredPosition = new Vector2(0, 80);
            var background = CreateImage(sliderObject.transform, "Background", new Color(0.18f, 0.18f, 0.18f));
            Stretch(background.rectTransform);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>());
            var fill = CreateImage(fillArea.transform, "Fill", new Color(0.62f, 0.12f, 0.1f));
            Stretch(fill.rectTransform);
            var slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.value = 1f;
            var loot = CreateText(canvas.transform, "Loot", "击败妖兽后将在这里显示随机装备词条", 38, new Vector2(0, -380), new Vector2(900, 620));
            loot.alignment = TextAnchor.UpperLeft;
            var equipLatest = CreateButton(canvas.transform, "EquipLatestButton", "穿戴最新装备", new Vector2(-180, -720), 380, 90);
            var guide = CreateText(canvas.transform, "GuideText", "弱引导：首场战斗会自动开始", 26, new Vector2(-80, -790), new Vector2(700, 70));
            guide.color = new Color(0.75f, 0.85f, 0.65f);

            var battlePage = new GameObject("BattlePage", typeof(RectTransform));
            battlePage.tag = "Finish";
            battlePage.transform.SetParent(canvas.transform, false);
            battlePage.transform.SetAsFirstSibling();

            string[] pages = { "CharacterPage", "EquipmentPage", "InventoryPage", "CultivationPage", "SpiritualRootPage", "StagePage", "ShopPage", "RankingPage", "MailPage", "TaskPage", "ActivityPage", "DebugPage" };
            string[] summaries = {
                "基础属性、统一战力与 Build 汇总", "十槽装备、随机词条、锁定与属性差异", "装备 / 材料 / 消耗品 · 筛选与一键分解",
                "境界突破、渡劫、主修与两项辅助功法", "金木水火土风雷阴阳 · 渡劫随机成长", "1-1 ～ 1-10 Boss · 连续推关",
                "普通 / 每日 / 礼包 / 境界商品 · 服务器定价", "战力榜 · 境界榜 · 推图榜", "系统邮件 · 附件 · 到期时间",
                "登录 / 推图 / Boss / 强化 / 分解 / 渡劫\n活跃宝箱 20 / 40 / 60 / 80 / 100", "太初开服周 · 双倍挂机时间窗", "开发环境专用：资源、境界、装备、离线与 Mock 支付"
            };
            for (var i = 0; i < pages.Length; i++) CreatePage(canvas.transform, pages[i], summaries[i]);

            var bottom = CreateImage(canvas.transform, "BottomNavigation", new Color(0.06f, 0.09f, 0.13f, 0.98f));
            bottom.rectTransform.sizeDelta = new Vector2(1080, 160);
            bottom.rectTransform.anchoredPosition = new Vector2(0, -880);
            CreateButton(bottom.transform, "Nav_BattlePage", "战斗", new Vector2(-450, 0), 160, 90);
            CreateButton(bottom.transform, "Nav_CharacterPage", "角色", new Vector2(-270, 0), 160, 90);
            CreateButton(bottom.transform, "Nav_InventoryPage", "背包", new Vector2(-90, 0), 160, 90);
            CreateButton(bottom.transform, "Nav_EquipmentPage", "装备", new Vector2(90, 0), 160, 90);
            CreateButton(bottom.transform, "Nav_CultivationPage", "修炼", new Vector2(270, 0), 160, 90);
            CreateButton(bottom.transform, "Nav_ShopPage", "商店", new Vector2(450, 0), 160, 90);

            var side = CreateImage(canvas.transform, "SideNavigation", new Color(0.06f, 0.09f, 0.13f, 0.94f));
            side.rectTransform.sizeDelta = new Vector2(190, 700);
            side.rectTransform.anchoredPosition = new Vector2(440, 220);
            CreateButton(side.transform, "Nav_TaskPage", "任务", new Vector2(0, 250), 160, 90);
            CreateButton(side.transform, "Nav_MailPage", "邮件", new Vector2(0, 140), 160, 90);
            CreateButton(side.transform, "Nav_RankingPage", "排行", new Vector2(0, 30), 160, 90);
            CreateButton(side.transform, "Nav_ActivityPage", "活动", new Vector2(0, -80), 160, 90);
            CreateButton(side.transform, "Nav_StagePage", "关卡", new Vector2(0, -190), 160, 90);
            CreateButton(side.transform, "Nav_DebugPage", "设置", new Vector2(0, -300), 160, 90);

            var login = CreateImage(canvas.transform, "LoginPage", new Color(0.025f, 0.04f, 0.065f, 1f));
            Stretch(login.rectTransform);
            login.transform.SetAsLastSibling();
            var loginTitle = CreateText(login.transform, "LoginTitle", "《太初：无尽轮回》\n一念入青崖，万器皆有缘", 54, new Vector2(0, 240), new Vector2(900, 300));
            loginTitle.color = new Color(0.9f, 0.79f, 0.43f);
            CreateButton(login.transform, "EnterGameButton", "离线演示 · 开始修行", new Vector2(0, -120), 620, 110);
            var serverLogin = CreateButton(login.transform, "ServerLoginButton", "本地服务器登录", new Vector2(0, -260), 620, 110);
            var loginFeedback = CreateText(login.transform, "LoginFeedback", "服务器模式需要先启动 ASP.NET Core（127.0.0.1:5080）", 24, new Vector2(0, -390), new Vector2(800, 100));
            var loginController = canvas.AddComponent<PrototypeLoginController>();
            var loginSerialized = new SerializedObject(loginController);
            loginSerialized.FindProperty("serverLoginButton").objectReferenceValue = serverLogin;
            loginSerialized.FindProperty("feedbackText").objectReferenceValue = loginFeedback;
            loginSerialized.ApplyModifiedPropertiesWithoutUndo();

            canvas.AddComponent<PrototypeNavigationController>();

            var controller = new GameObject("GameController").AddComponent<PrototypeGameController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("lootText").objectReferenceValue = loot;
            serialized.FindProperty("enemyHealth").objectReferenceValue = slider;
            serialized.FindProperty("profileText").objectReferenceValue = profile;
            serialized.FindProperty("currencyText").objectReferenceValue = GameObject.Find("Currencies").GetComponent<Text>();
            serialized.FindProperty("guideText").objectReferenceValue = guide;
            serialized.FindProperty("equipLatestButton").objectReferenceValue = equipLatest;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            System.IO.Directory.CreateDirectory("Assets/Game/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Game/Scenes/Main.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Game/Scenes/Main.unity", true) };
            AssetDatabase.SaveAssets();
            Debug.Log("ImmortalLoot prototype scene generated successfully.");
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void CreatePage(Transform parent, string name, string summary)
        {
            var panel = CreateImage(parent, name, new Color(0.055f, 0.08f, 0.11f, 0.97f));
            panel.gameObject.tag = "Finish";
            panel.rectTransform.sizeDelta = new Vector2(820, 1180);
            panel.rectTransform.anchoredPosition = new Vector2(-70, -20);
            var text = CreateText(panel.transform, name + "Content", summary + "\n\n领域逻辑与服务器接口已就绪\n此处为 MVP 统一占位页面", 34, Vector2.zero, new Vector2(700, 900));
            text.alignment = TextAnchor.UpperLeft;
            CreateButton(panel.transform, "Action_" + name, "执行 / 刷新", new Vector2(0, -470), 360, 90);
            panel.gameObject.SetActive(false);
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, float width = 180, float height = 110)
        {
            var image = CreateImage(parent, name, new Color(0.16f, 0.24f, 0.30f));
            image.rectTransform.sizeDelta = new Vector2(width, height);
            image.rectTransform.anchoredPosition = position;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateText(image.transform, "Label", label, 28, Vector2.zero, new Vector2(width, height));
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
